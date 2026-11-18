using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class VRConstructionController : MonoBehaviour
{
    private enum ConstructionState { Idle, Building }

    [Header("������ �� ����������")]
    public VRRoadBuilder roadBuilder;
    public BuildManager buildManager;
    public Transform handTransform;
    public InputActionReference triggerAction;

    [Header("L1/R1 ��������")]
    public InputActionAsset inputActions;
    public float rotationStep = 15f;

    [Header("��������� ����")]
    public float maxBuildDistance = 50f;

    private ConstructionState currentState = ConstructionState.Idle;
    private List<Vector3> buildPoints = new List<Vector3>();
    private bool wasTriggerPressedLastFrame = false;
    private float initialBuildHeight = 0f;
    private Vector3 lastValidHitPoint;
    private const float MinStretchLength = 2f;
    private const float MaxStretchLength = 30f;
    private const float StretchStep = 0.1f;
    private const float TurnDeadZone = 0.08f;
    private const float TurnDegreesPerMeter = 180f;
    private const float MaxTurnDegrees = 90f;
    private Vector3 stretchHandOrigin;
    private Vector3 stretchForward;
    private float stretchInitialLength;
    private BuildableItem stretchItem;
    private int stretchLanes = 1;
    private float stretchLaneWidth;
    private float stretchAngle;
    private float stretchLength;
    private SnapPoint startSnap;
    private SnapPoint endSnap;
    private GameObject singleGhost;
    private float singleRotationOffset = 0f;
    private bool wasRotateLeftPressed = false;
    private bool wasRotateRightPressed = false;
    private InputAction rotateLeftAction;
    private InputAction rotateRightAction;

    private VRMenuController menuController;
    private bool waitForTriggerRelease;

    private void Awake()
    {
        menuController = GetComponent<VRMenuController>();

        if (inputActions == null)
        {
            if (triggerAction != null && triggerAction.action != null)
                inputActions = triggerAction.action.actionMap.asset;
        }

        if (inputActions == null)
        {
            InputActionAsset[] assets = Resources.FindObjectsOfTypeAll<InputActionAsset>();
            if (assets.Length > 0)
                inputActions = assets[0];
        }

        if (inputActions != null)
        {
            var map = inputActions.FindActionMap("XRI Construction");
            if (map != null)
            {
                rotateLeftAction = map.FindAction("L1");
                rotateRightAction = map.FindAction("R1");
                rotateLeftAction?.Enable();
                rotateRightAction?.Enable();
            }
        }
    }

    private void OnEnable()
    {
        triggerAction?.action?.Enable();
        rotateLeftAction?.Enable();
        rotateRightAction?.Enable();
        if (handTransform == null)
            Debug.LogError("Construction requires a handTransform. Assign the controller ray origin in the Inspector.", this);
    }

    private void Update()
    {
        if (buildManager == null || roadBuilder == null || buildManager.CurrentItem == null ||
            triggerAction == null || triggerAction.action == null || handTransform == null)
        {
            return;
        }

        bool isTriggerHeld = triggerAction.action.IsPressed() || triggerAction.action.ReadValue<float>() > 0.5f;

        if (!buildManager.CurrentItem.isSplineType)
        {
            bool leftDown = rotateLeftAction != null && rotateLeftAction.IsPressed();
            bool rightDown = rotateRightAction != null && rotateRightAction.IsPressed();

            if (leftDown && !wasRotateLeftPressed)
                singleRotationOffset -= rotationStep;
            if (rightDown && !wasRotateRightPressed)
                singleRotationOffset += rotationStep;

            wasRotateLeftPressed = leftDown;
            wasRotateRightPressed = rightDown;
        }
        else
        {
            wasRotateLeftPressed = false;
            wasRotateRightPressed = false;
        }

        if (menuController != null && menuController.menuCanvas != null && menuController.menuCanvas.activeSelf)
        {
            if (currentState == ConstructionState.Building) CancelConstruction();
            else roadBuilder.ClearGhostRoad();
            ClearSingleGhost();
            singleRotationOffset = 0f;
            wasTriggerPressedLastFrame = isTriggerHeld;
            return;
        }

        if (waitForTriggerRelease)
        {
            wasTriggerPressedLastFrame = isTriggerHeld;
            if (isTriggerHeld) return;
            waitForTriggerRelease = false;
        }

        BuildableItem currentItem = buildManager.CurrentItem;

        if (stretchItem != null && stretchItem != currentItem)
            CancelConstruction();

        if (isTriggerHeld && !wasTriggerPressedLastFrame)
        {
            if (currentItem.isSplineType)
            {
                buildPoints.Clear();

                if (TryGetBuildStart(currentItem, out Vector3 hitPoint, out startSnap))
                {
                    endSnap = null;
                    currentState = ConstructionState.Building;
                    lastValidHitPoint = hitPoint;
                    buildPoints.Add(hitPoint);
                    initialBuildHeight = hitPoint.y;
                    if (currentItem.stretchWithController)
                    {
                        stretchItem = currentItem;
                        stretchLanes = buildManager.RoadLanes;
                        stretchLaneWidth = roadBuilder.GetLaneWidth(currentItem);
                        stretchHandOrigin = handTransform.position;
                        stretchForward = GetStretchForward(startSnap);
                        stretchInitialLength = Mathf.Clamp(roadBuilder.GetSegmentLength(currentItem),
                            MinStretchLength, MaxStretchLength);
                        UpdateStretch(true);
                    }
                }
            }
            else
            {
                HandleSinglePlacement(currentItem);
            }
        }

        if (!currentItem.isSplineType)
        {
            UpdateSingleGhost(currentItem);
        }

        if (currentState == ConstructionState.Idle && currentItem.isSplineType)
        {
            if (TryGetBuildStart(currentItem, out Vector3 cursor, out SnapPoint target))
            {
                if (currentItem.stretchWithController)
                {
                    float length = Mathf.Clamp(roadBuilder.GetSegmentLength(currentItem),
                        MinStretchLength, MaxStretchLength);
                    roadBuilder.DrawStretchGhost(cursor, cursor + GetStretchForward(target) * length,
                        currentItem, buildManager.RoadLanes);
                }
                roadBuilder.ShowSnapIndicator(target);
            }
            else roadBuilder.ClearGhostRoad();
        }

        if (wasTriggerPressedLastFrame && currentState == ConstructionState.Building)
        {
            if (stretchItem != null)
                UpdateStretch(isTriggerHeld);
            else if (currentItem.isSplineType && TryGetBuildEnd(currentItem, out Vector3 currentHitPoint, out endSnap))
            {
                Vector3 offset = Vector3.up * currentItem.heightOffset;
                Vector3 origin = buildPoints[buildPoints.Count - 1];
                bool alignStart = buildPoints.Count == 1 && startSnap != null && startSnap.Available;
                if (alignStart && !roadBuilder.CanAlignSnap(currentHitPoint - origin, startSnap.Forward))
                {
                    endSnap = null;
                    lastValidHitPoint = origin;
                    roadBuilder.ClearGhostRoad();
                    if (!isTriggerHeld)
                    {
                        CancelConstruction();
                        wasTriggerPressedLastFrame = false;
                    }
                    return;
                }
                if (endSnap != null)
                    currentHitPoint = endSnap.Position - offset;
                else if (alignStart)
                    currentHitPoint = origin + startSnap.Forward *
                        Mathf.Max(0f, Vector3.Dot(currentHitPoint - origin, startSnap.Forward));
                else if (currentItem.singleSpan)
                    currentHitPoint.y = initialBuildHeight;

                lastValidHitPoint = currentHitPoint;

                if (currentItem.singleSpan)
                {
                    if (buildPoints.Count > 0)
                    {
                        List<Vector3> preview = new List<Vector3> { buildPoints[0], lastValidHitPoint };
                        roadBuilder.DrawGhostRoad(preview, currentItem);
                        roadBuilder.ShowSnapIndicator(endSnap != null ? endSnap : startSnap, true);
                    }
                }
                else
                {
                    Vector3 lastFixedPoint = buildPoints[buildPoints.Count - 1];
                    float segmentLength = roadBuilder.GetSegmentLength(currentItem);
                    if (Vector3.Distance(lastFixedPoint, currentHitPoint) >= segmentLength && endSnap == null)
                    {
                        Vector3 dirXZ = (currentHitPoint - lastFixedPoint);
                        dirXZ.y = 0;
                        if (dirXZ.sqrMagnitude < 0.0001f) dirXZ = Vector3.forward;
                        dirXZ.Normalize();

                        Vector3 nextPoint = lastFixedPoint + ((alignStart ? startSnap.Forward : dirXZ) * segmentLength);

                        if (!alignStart && currentItem.snapToTerrain)
                            nextPoint.y = roadBuilder.SampleTerrainHeight(nextPoint);

                        buildPoints.Add(nextPoint);
                    }

                    List<Vector3> previewPoints = new List<Vector3>(buildPoints);
                    if (Vector3.Distance(previewPoints[previewPoints.Count - 1], currentHitPoint) >= 0.01f)
                    {
                        previewPoints.Add(currentHitPoint);
                    }

                    roadBuilder.DrawGhostRoad(previewPoints, currentItem);
                    roadBuilder.ShowSnapIndicator(endSnap != null ? endSnap : startSnap, true);
                }
            }

            else
            {
                endSnap = null;
                lastValidHitPoint = buildPoints[buildPoints.Count - 1];
                roadBuilder.ClearGhostRoad();
            }
        }

        if (!isTriggerHeld && wasTriggerPressedLastFrame)
        {
            if (currentState == ConstructionState.Building)
            {
                currentState = ConstructionState.Idle;

                if (stretchItem != null && stretchAngle != 0f)
                    roadBuilder.PlaceRoadArc(buildPoints[0], stretchForward, stretchLength, stretchAngle,
                        stretchItem, stretchLanes, stretchLaneWidth, startSnap, false);
                else
                {
                    if (buildPoints.Count > 0 &&
                        Vector3.Distance(buildPoints[buildPoints.Count - 1], lastValidHitPoint) >= 0.01f)
                        buildPoints.Add(lastValidHitPoint);

                    roadBuilder.BuildFinalRoad(buildPoints, currentItem, startSnap, endSnap,
                        stretchItem != null ? stretchLanes : 1);
                }
                stretchItem = null;
                startSnap = endSnap = null;
                buildPoints.Clear();
            }
        }

        wasTriggerPressedLastFrame = isTriggerHeld;
    }

    private bool TryGetBuildStart(BuildableItem item, out Vector3 cursor, out SnapPoint target)
    {
        bool hitGround = GetLaserHitPoint(out cursor, out _);
        Ray ray = new Ray(handTransform.position, handTransform.forward);
        target = roadBuilder.FindAimedSnapPoint(ray, GetSnapReach(ray, hitGround, cursor));

        Vector3 offset = Vector3.up * item.heightOffset;
        if (target == null && hitGround)
            target = roadBuilder.FindNearestSnapPoint(cursor + offset);
        if (target != null)
            cursor = target.Position - offset;
        return target != null || hitGround;
    }

    private bool TryGetBuildEnd(BuildableItem item, out Vector3 cursor, out SnapPoint target)
    {
        Vector3 offset = Vector3.up * item.heightOffset;
        Vector3 origin = buildPoints[buildPoints.Count - 1] + offset;
        bool alignStart = buildPoints.Count == 1 && startSnap != null && startSnap.Available;
        bool hitGround = GetLaserHitPoint(out cursor, out _);
        Ray ray = new Ray(handTransform.position, handTransform.forward);
        target = roadBuilder.FindAimedSnapPoint(ray, GetSnapReach(ray, hitGround, cursor),
            startSnap, candidate =>
            {
                Vector3 direction = candidate.Position - origin;
                return SnapPoint.HasMatchingAxis(direction, -candidate.Forward) &&
                    (!alignStart || SnapPoint.HasMatchingAxis(direction, startSnap.Forward));
            });

        if (target == null && hitGround)
            target = roadBuilder.FindNearestSnapPoint(cursor + offset, startSnap,
                origin, alignStart ? startSnap.Forward : Vector3.zero);
        if (target != null)
            cursor = target.Position - offset;
        if (target == null && alignStart && item.singleSpan)
        {
            // Aim at the continuation of the deck, including above the terrain.
            Vector3 axis = startSnap.Forward;
            Vector3 delta = ray.origin - origin;
            float alignment = Vector3.Dot(ray.direction, axis);
            float denominator = 1f - alignment * alignment;
            if (denominator > 0.0001f)
            {
                float distanceAlong = (Vector3.Dot(delta, axis) -
                    alignment * Vector3.Dot(delta, ray.direction)) / denominator;
                Vector3 pointOnAxis = origin + axis * distanceAlong;
                float rayDepth = Vector3.Dot(pointOnAxis - ray.origin, ray.direction);
                float miss = Vector3.Distance(pointOnAxis, ray.GetPoint(rayDepth));
                if (distanceAlong >= 0.1f && rayDepth > 0f &&
                    rayDepth <= GetSnapReach(ray, hitGround, cursor) && miss <= roadBuilder.snapRadius)
                {
                    cursor = pointOnAxis - offset;
                    return true;
                }
            }
        }
        return target != null || hitGround;
    }

    private float GetSnapReach(Ray ray, bool hitGround, Vector3 cursor)
    {
        return hitGround
            ? Mathf.Min(maxBuildDistance, Vector3.Distance(ray.origin, cursor) + roadBuilder.snapRadius)
            : maxBuildDistance;
    }

    private Vector3 GetStretchForward(SnapPoint target)
    {
        if (target != null) return target.Forward;
        Vector3 forward = Vector3.ProjectOnPlane(handTransform.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.ProjectOnPlane(handTransform.up, Vector3.up);
        return forward.normalized;
    }

    private void UpdateStretch(bool preview)
    {
        Vector3 movement = handTransform.position - stretchHandOrigin;
        float displacement = Vector3.Dot(movement, stretchForward);
        float length = Mathf.Clamp(stretchInitialLength + displacement, MinStretchLength, MaxStretchLength);
        stretchLength = preview ? length : RoundStretchLength(length);

        Vector3 right = Vector3.Cross(Vector3.up, stretchForward).normalized;
        float lateral = Vector3.Dot(movement, right);
        float steering = Mathf.Sign(lateral) * Mathf.Max(0f, Mathf.Abs(lateral) - TurnDeadZone);
        // Leave a positive inner radius even for the widest road.
        float minimumRadius = stretchLaneWidth * stretchLanes * 0.75f;
        float maxAngle = Mathf.Min(MaxTurnDegrees, stretchLength /
            Mathf.Max(minimumRadius, 0.01f) * Mathf.Rad2Deg);
        stretchAngle = stretchLaneWidth > 0f ?
            Mathf.Clamp(steering * TurnDegreesPerMeter, -maxAngle, maxAngle) : 0f;
        if (Mathf.Abs(stretchAngle) < 0.5f) stretchAngle = 0f;

        if (stretchAngle != 0f)
        {
            endSnap = null;
            if (preview)
            {
                roadBuilder.PlaceRoadArc(buildPoints[0], stretchForward, stretchLength, stretchAngle,
                    stretchItem, stretchLanes, stretchLaneWidth, startSnap, true);
                roadBuilder.ShowSnapIndicator(startSnap, true);
            }
            return;
        }

        Vector3 offset = Vector3.up * stretchItem.heightOffset;
        Vector3 origin = buildPoints[0];
        Vector3 worldStart = origin + offset;
        lastValidHitPoint = origin + stretchForward * length;
        endSnap = roadBuilder.FindNearestSnapPoint(lastValidHitPoint + offset, startSnap,
            worldStart, stretchForward, candidate =>
            {
                Vector3 delta = candidate.Position - worldStart;
                float axialLength = Vector3.Dot(delta, stretchForward);
                return axialLength >= MinStretchLength - 0.001f && axialLength <= MaxStretchLength + 0.001f &&
                    (delta - stretchForward * axialLength).sqrMagnitude <= 0.000001f;
            });
        if (endSnap != null)
            lastValidHitPoint = endSnap.Position - offset;
        else if (!preview)
            lastValidHitPoint = origin + stretchForward * RoundStretchLength(length);

        if (preview)
        {
            roadBuilder.DrawStretchGhost(buildPoints[0], lastValidHitPoint, stretchItem, stretchLanes);
            roadBuilder.ShowSnapIndicator(endSnap != null ? endSnap : startSnap, true);
        }
    }

    private static float RoundStretchLength(float length)
    {
        float steps = Mathf.Round((length - MinStretchLength) / StretchStep);
        return Mathf.Clamp(MinStretchLength + steps * StretchStep, MinStretchLength, MaxStretchLength);
    }

    private void CancelConstruction()
    {
        currentState = ConstructionState.Idle;
        buildPoints.Clear();
        startSnap = endSnap = null;
        stretchItem = null;
        if (roadBuilder != null) roadBuilder.ClearGhostRoad();
    }

    // Ignore the menu click until the trigger has been released.
    public void PrepareForSelection()
    {
        CancelConstruction();
        ClearSingleGhost();
        singleRotationOffset = 0f;
        waitForTriggerRelease = true;
    }

    public void ResetSingleRotation()
    {
        singleRotationOffset = 0f;
    }

    private bool GetLaserHitPoint(out Vector3 hitPoint, out Vector3 hitNormal)
    {
        Ray ray = new Ray(handTransform.position, handTransform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, maxBuildDistance, roadBuilder.terrainLayer))
        {
            hitPoint = hit.point;
            hitNormal = hit.normal;
            return true;
        }
        hitPoint = Vector3.zero;
        hitNormal = Vector3.up;
        return false;
    }

    private void HandleSinglePlacement(BuildableItem item)
    {
        if (GetLaserHitPoint(out Vector3 spawnPoint, out Vector3 groundNormal))
        {
            if (item.requireRoadProximity && roadBuilder.DistanceToNearestRoad(spawnPoint) > item.maxRoadDistance)
            {
                Debug.LogWarning("Cannot place " + item.itemName + ": too far from nearest road");
                return;
            }

            Quaternion spawnRotation = Quaternion.FromToRotation(Vector3.up, groundNormal);
            Vector3 lookDirection = handTransform.position - spawnPoint;
            lookDirection.y = 0;
            if (lookDirection != Vector3.zero)
            {
                Quaternion facePlayerRot = Quaternion.LookRotation(lookDirection);
                spawnRotation = facePlayerRot * Quaternion.FromToRotation(Vector3.up, groundNormal);
            }

            spawnRotation *= Quaternion.Euler(0, singleRotationOffset, 0);

            Instantiate(item.finalPrefab, spawnPoint, spawnRotation);
            singleRotationOffset = 0f;
            roadBuilder.ClearGhostRoad();
        }
    }

    private void UpdateSingleGhost(BuildableItem item)
    {
        if (!GetLaserHitPoint(out Vector3 hitPoint, out Vector3 groundNormal))
        {
            ClearSingleGhost();
            return;
        }

        if (singleGhost == null)
        {
            GameObject prefab = item.ghostPrefab != null ? item.ghostPrefab : item.finalPrefab;
            if (prefab == null) return;
            singleGhost = Instantiate(prefab);
            foreach (var col in singleGhost.GetComponentsInChildren<Collider>())
                Destroy(col);
        }

        Quaternion rotation = Quaternion.FromToRotation(Vector3.up, groundNormal);
        Vector3 lookDir = handTransform.position - hitPoint;
        lookDir.y = 0;
        if (lookDir != Vector3.zero)
        {
            Quaternion facePlayer = Quaternion.LookRotation(lookDir);
            rotation = facePlayer * Quaternion.FromToRotation(Vector3.up, groundNormal);
        }

        singleGhost.transform.position = hitPoint;
        rotation *= Quaternion.Euler(0, singleRotationOffset, 0);
        singleGhost.transform.rotation = rotation;

        if (item.requireRoadProximity)
        {
            bool tooFar = roadBuilder.DistanceToNearestRoad(hitPoint) > item.maxRoadDistance;
            foreach (var r in singleGhost.GetComponentsInChildren<MeshRenderer>())
                r.material.color = tooFar ? Color.red : Color.white;
        }
    }

    private void ClearSingleGhost()
    {
        if (singleGhost != null)
        {
            Destroy(singleGhost);
            singleGhost = null;
        }
    }

    private void OnDisable()
    {
        rotateLeftAction?.Disable();
        rotateRightAction?.Disable();
        CancelConstruction();
        ClearSingleGhost();
        wasTriggerPressedLastFrame = false;
    }
}