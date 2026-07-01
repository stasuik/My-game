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

    [Header("��������� ����")]
    public float maxBuildDistance = 50f;

    private ConstructionState currentState = ConstructionState.Idle;
    private List<Vector3> buildPoints = new List<Vector3>();
    private bool wasTriggerPressedLastFrame = false;
    private float initialBuildHeight = 0f;
    private Vector3 lastValidHitPoint;

    private VRMenuController menuController;

    private void Awake()
    {
        menuController = GetComponent<VRMenuController>();
    }

    private void Update()
    {
        if (buildManager == null || roadBuilder == null || buildManager.CurrentItem == null ||
            triggerAction == null || triggerAction.action == null || handTransform == null)
        {
            return;
        }

        bool isTriggerHeld = triggerAction.action.IsPressed() || triggerAction.action.ReadValue<float>() > 0.5f;

        if (menuController != null && menuController.menuCanvas != null && menuController.menuCanvas.activeSelf)
        {
            if (currentState == ConstructionState.Building) CancelConstruction();
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
                        Vector3 direction = (currentHitPoint - lastFixedPoint).normalized;
                        Vector3 nextPoint = lastFixedPoint + (direction * currentItem.stepDistance);

                        if (currentItem.snapToTerrain)
                        {
                            nextPoint = GetTerrainHeightAt(nextPoint);
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

    private Vector3 GetTerrainHeightAt(Vector3 originPos)
    {
        Vector3 rayStart = new Vector3(originPos.x, originPos.y + 50f, originPos.z);
        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 100f, roadBuilder.terrainLayer))
        {
            return hit.point;
        }
        return originPos;
    }

    private void HandleSinglePlacement(BuildableItem item)
    {
        if (GetLaserHitPoint(out Vector3 spawnPoint, out Vector3 groundNormal))
        {
            Quaternion spawnRotation = Quaternion.FromToRotation(Vector3.up, groundNormal);
            Vector3 lookDirection = handTransform.position - spawnPoint;
            lookDirection.y = 0;
            if (lookDirection != Vector3.zero)
            {
                Quaternion facePlayerRot = Quaternion.LookRotation(lookDirection);
                spawnRotation = facePlayerRot * Quaternion.FromToRotation(Vector3.up, groundNormal);
            }

            Instantiate(item.finalPrefab, spawnPoint, spawnRotation);
            roadBuilder.ClearGhostRoad();
        }
    }

    private void OnDisable()
    {
        CancelConstruction();
        wasTriggerPressedLastFrame = false;
    }
}