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
    private GameObject singleGhost;
    private float singleRotationOffset = 0f;
    private bool wasRotateLeftPressed = false;
    private bool wasRotateRightPressed = false;
    private InputAction rotateLeftAction;
    private InputAction rotateRightAction;

    private VRMenuController menuController;

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
            ClearSingleGhost();
            singleRotationOffset = 0f;
            wasTriggerPressedLastFrame = isTriggerHeld;
            return;
        }

        BuildableItem currentItem = buildManager.CurrentItem;

        if (isTriggerHeld && !wasTriggerPressedLastFrame)
        {
            if (currentItem.isSplineType)
            {
                currentState = ConstructionState.Building;
                buildPoints.Clear();

                if (GetLaserHitPoint(out Vector3 hitPoint, out _))
                {
                    buildPoints.Add(hitPoint);
                    initialBuildHeight = hitPoint.y;
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

        if (isTriggerHeld && wasTriggerPressedLastFrame && currentState == ConstructionState.Building)
        {
            if (currentItem.isSplineType && GetLaserHitPoint(out Vector3 currentHitPoint, out _))
            {
                if (!currentItem.snapToTerrain)
                {
                    currentHitPoint.y = initialBuildHeight;
                }

                lastValidHitPoint = currentHitPoint;

                if (currentItem.singleSpan)
                {
                    if (buildPoints.Count > 0)
                    {
                        List<Vector3> preview = new List<Vector3> { buildPoints[0], lastValidHitPoint };
                        roadBuilder.DrawGhostRoad(preview, currentItem);
                    }
                }
                else
                {
                    Vector3 lastFixedPoint = buildPoints[buildPoints.Count - 1];
                    float distanceXZ = Vector2.Distance(new Vector2(lastFixedPoint.x, lastFixedPoint.z),
                                                        new Vector2(currentHitPoint.x, currentHitPoint.z));

                    if (distanceXZ >= currentItem.stepDistance)
                    {
                        Vector3 dirXZ = (currentHitPoint - lastFixedPoint);
                        dirXZ.y = 0;
                        if (dirXZ.sqrMagnitude < 0.0001f) dirXZ = Vector3.forward;
                        dirXZ.Normalize();

                        Vector3 nextPoint = lastFixedPoint + (dirXZ * currentItem.stepDistance);

                        if (currentItem.snapToTerrain)
                        {
                            nextPoint.y = roadBuilder.SampleTerrainHeight(nextPoint);
                        }
                        else
                        {
                            nextPoint.y = initialBuildHeight;
                        }

                        buildPoints.Add(nextPoint);
                    }

                    List<Vector3> previewPoints = new List<Vector3>(buildPoints);
                    if (Vector3.Distance(lastFixedPoint, currentHitPoint) > 0.2f)
                    {
                        previewPoints.Add(currentHitPoint);
                    }
                    else if (previewPoints.Count == 1)
                    {
                        previewPoints.Add(currentHitPoint + new Vector3(0.01f, 0, 0));
                    }

                    roadBuilder.DrawGhostRoad(previewPoints, currentItem);
                }
            }

            if (currentItem.requireRoadProximity && !currentItem.isSplineType)
            {
                bool tooFar = roadBuilder.DistanceToNearestRoad(lastValidHitPoint) > currentItem.maxRoadDistance;
                roadBuilder.SetGhostColor(tooFar ? Color.red : Color.white);
            }
        }

        if (!isTriggerHeld && wasTriggerPressedLastFrame)
        {
            if (currentState == ConstructionState.Building)
            {
                currentState = ConstructionState.Idle;

                if (currentItem.singleSpan && buildPoints.Count > 0)
                {
                    Vector3 endPoint = lastValidHitPoint;
                    if (!currentItem.snapToTerrain) endPoint.y = initialBuildHeight;
                    buildPoints.Add(endPoint);
                }

                roadBuilder.BuildFinalRoad(buildPoints, currentItem);
                buildPoints.Clear();
            }
        }

        wasTriggerPressedLastFrame = isTriggerHeld;
    }

    private void CancelConstruction()
    {
        currentState = ConstructionState.Idle;
        buildPoints.Clear();
        if (roadBuilder != null) roadBuilder.ClearGhostRoad();
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