﻿namespace VRTK
{
    using UnityEngine;
    using UnityEngine.EventSystems;
    using UnityEngine.UI;
    using System.Collections.Generic;

    public class VRTK_VRInputModule : PointerInputModule
    {
        public List<VRTK_UIPointer> pointers = new List<VRTK_UIPointer>();

        public virtual void Initialise()
        {
            pointers.Clear();
        }

        //Needed to allow other regular (non-VR) InputModules in combination with VRTK_EventSystem
        public override bool IsModuleSupported()
        {
            return false;
        }

        public override void Process()
        {
            for (int i = 0; i < pointers.Count; i++)
            {
                VRTK_UIPointer pointer = pointers[i];
                if (pointer.gameObject.activeInHierarchy && pointer.enabled)
                {
                    List<RaycastResult> results = new List<RaycastResult>();
                    if (pointer.PointerActive())
                    {
                        results = CheckRaycasts(pointer);
                    }

                    //Process events
                    Hover(pointer, results);
                    Click(pointer, results);
                    Drag(pointer, results);
                    Scroll(pointer, results);
                }
            }
        }

        protected virtual List<RaycastResult> CheckRaycasts(VRTK_UIPointer pointer)
        {
            RaycastResult raycastResult = new RaycastResult();
            raycastResult.worldPosition = pointer.GetOriginPosition();
            raycastResult.worldNormal = pointer.GetOriginForward();

            pointer.pointerEventData.pointerCurrentRaycast = raycastResult;

            List<RaycastResult> raycasts = new List<RaycastResult>();
            eventSystem.RaycastAll(pointer.pointerEventData, raycasts);

            //Debug.Log(raycasts.Count);
            //Debug.DrawRay(raycastResult.worldPosition, raycastResult.worldNormal);

            return raycasts;
        }

        protected virtual bool CheckTransformTree(Transform target, Transform source)
        {
            if (target == null)
            {
                return false;
            }

            if (target == source)
            {
                return true;
            }

            return CheckTransformTree(target.transform.parent, source);
        }

        protected virtual bool NoValidCollision(VRTK_UIPointer pointer, List<RaycastResult> results)
        {
            return (results.Count == 0 || !CheckTransformTree(results[0].gameObject.transform, pointer.pointerEventData.pointerEnter.transform));
        }

        protected virtual bool IsHovering(VRTK_UIPointer pointer)
        {
            for (int i = 0; i < pointer.pointerEventData.hovered.Count; i++)
            {
                GameObject hoveredObject = pointer.pointerEventData.hovered[i];
                if (pointer.pointerEventData.pointerEnter != null && hoveredObject != null && CheckTransformTree(hoveredObject.transform, pointer.pointerEventData.pointerEnter.transform))
                {
                    return true;
                }
            }
            return false;
        }

        protected virtual bool ValidElement(GameObject obj)
        {
            VRTK_UICanvas canvasCheck = obj.GetComponentInParent<VRTK_UICanvas>();

            return (canvasCheck != null && canvasCheck.enabled ? true : false);
        }

        protected virtual void CheckPointerHoverClick(VRTK_UIPointer pointer, List<RaycastResult> results)
        {
            if (pointer.hoverDurationTimer > 0f)
            {
                pointer.hoverDurationTimer -= Time.deltaTime;
            }

            if (pointer.canClickOnHover && pointer.hoverDurationTimer <= 0f)
            {
                pointer.canClickOnHover = false;
                ClickOnDown(pointer, results, true);
            }
        }

        protected virtual void Hover(VRTK_UIPointer pointer, List<RaycastResult> results)
        {
            if (pointer.pointerEventData.pointerEnter != null)
            {
                CheckPointerHoverClick(pointer, results);
                if (!ValidElement(pointer.pointerEventData.pointerEnter))
                {
                    pointer.pointerEventData.pointerEnter = null;
                    return;
                }

                if (NoValidCollision(pointer, results))
                {
                    ExecuteEvents.ExecuteHierarchy(pointer.pointerEventData.pointerEnter, pointer.pointerEventData, ExecuteEvents.pointerExitHandler);
                    pointer.pointerEventData.hovered.Remove(pointer.pointerEventData.pointerEnter);
                    pointer.pointerEventData.pointerEnter = null;
                }
            }
            else
            {
                for (int i = 0; i < results.Count; i++)
                {
                    RaycastResult result = results[i];
                    if (!ValidElement(result.gameObject))
                    {
                        continue;
                    }

                    //bug  当一个按钮父层存在可交互的背景时，焦点从背景移入按钮后，按钮无法接收hover事件。
                    GameObject target = ExecuteEvents.ExecuteHierarchy(result.gameObject, pointer.pointerEventData, ExecuteEvents.pointerEnterHandler);

                    // Debug.Log("ExecuteHierarchy1:" + target);

                    target = (target == null ? result.gameObject : target);

                    //Debug.Log("ExecuteHierarchy2:" + target);

                    if (target != null)
                    {
                        Selectable selectable = target.GetComponent<Selectable>();
                        if (selectable != null)
                        {
                            Navigation noNavigation = new Navigation();
                            noNavigation.mode = Navigation.Mode.None;
                            selectable.navigation = noNavigation;
                        }

                        if (pointer.hoveringElement != null && pointer.hoveringElement != target)
                        {
                            pointer.OnUIPointerElementExit(pointer.SetUIPointerEvent(result, null, pointer.hoveringElement));
                        }

                        pointer.OnUIPointerElementEnter(pointer.SetUIPointerEvent(result, target, pointer.hoveringElement));
                        pointer.hoveringElement = target;
                        pointer.pointerEventData.pointerCurrentRaycast = result;
                        pointer.pointerEventData.pointerEnter = target;
                        pointer.pointerEventData.hovered.Add(pointer.pointerEventData.pointerEnter);
                        break;
                    }

                    if (result.gameObject != pointer.hoveringElement)
                    {
                        pointer.OnUIPointerElementEnter(pointer.SetUIPointerEvent(result, result.gameObject, pointer.hoveringElement));
                    }
                    pointer.hoveringElement = result.gameObject;
                }

                if (pointer.hoveringElement && results.Count == 0)
                {
                    pointer.OnUIPointerElementExit(pointer.SetUIPointerEvent(new RaycastResult(), null, pointer.hoveringElement));
                    pointer.hoveringElement = null;
                }
            }
        }

        protected virtual void Click(VRTK_UIPointer pointer, List<RaycastResult> results)
        {
            switch (pointer.clickMethod)
            {
                case VRTK_UIPointer.ClickMethods.ClickOnButtonUp:
                    ClickOnUp(pointer, results);
                    break;
                case VRTK_UIPointer.ClickMethods.ClickOnButtonDown:
                    ClickOnDown(pointer, results);
                    break;
            }
        }

        protected virtual void ClickOnUp(VRTK_UIPointer pointer, List<RaycastResult> results)
        {
            pointer.pointerEventData.eligibleForClick = pointer.ValidClick(false);

            if (!AttemptClick(pointer) && !IsDragElement(pointer))
            {
                IsEligibleClick(pointer, results);
            }
        }

        protected virtual void ClickOnDown(VRTK_UIPointer pointer, List<RaycastResult> results, bool forceClick = false)
        {
            pointer.pointerEventData.eligibleForClick = (forceClick ? true : pointer.ValidClick(true));

            if (IsEligibleClick(pointer, results))
            {
                pointer.pointerEventData.eligibleForClick = false;
                AttemptClick(pointer);
            }
        }

        protected virtual bool IsEligibleClick(VRTK_UIPointer pointer, List<RaycastResult> results)
        {
            if (pointer.pointerEventData.eligibleForClick)
            {
                for (int i = 0; i < results.Count; i++)
                {
                    RaycastResult result = results[i];

                    if (!ValidElement(result.gameObject))
                    {
                        continue;
                    }

                    GameObject target = ExecuteEvents.ExecuteHierarchy(result.gameObject, pointer.pointerEventData, ExecuteEvents.pointerDownHandler);

                    if (target != null)
                    {
                        pointer.pointerEventData.pressPosition = pointer.pointerEventData.position;
                        pointer.pointerEventData.pointerPressRaycast = result;
                        pointer.pointerEventData.pointerPress = target;

                        return true;
                    }
                }
            }

            return false;
        }

        #region fix

        private bool IsDragElement(VRTK_UIPointer pointer)
        {
            if (pointer.pointerEventData.pointerDrag != null
                && pointer.pointerEventData.pointerDrag.GetComponent<EventTrigger>() == null)
            {
                return true;
            }

            return false;
        }

        #endregion

        protected virtual bool AttemptClick(VRTK_UIPointer pointer)
        {
            if (pointer.pointerEventData.pointerPress)
            {
                if (!ValidElement(pointer.pointerEventData.pointerPress))
                {
                    pointer.pointerEventData.pointerPress = null;
                    return true;
                }

                if (pointer.pointerEventData.eligibleForClick)
                {
                    if (!IsHovering(pointer) || IsDragElement(pointer))
                    {
                        ExecuteEvents.ExecuteHierarchy(pointer.pointerEventData.pointerPress, pointer.pointerEventData, ExecuteEvents.pointerUpHandler);
                        pointer.pointerEventData.pointerPress = null;
                    }
                }
                else
                {
                    pointer.OnUIPointerElementClick(pointer.SetUIPointerEvent(pointer.pointerEventData.pointerPressRaycast, pointer.pointerEventData.pointerPress));
                    ExecuteEvents.ExecuteHierarchy(pointer.pointerEventData.pointerPress, pointer.pointerEventData, ExecuteEvents.pointerClickHandler);
                    ExecuteEvents.ExecuteHierarchy(pointer.pointerEventData.pointerPress, pointer.pointerEventData, ExecuteEvents.pointerUpHandler);
                    pointer.pointerEventData.pointerPress = null;
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// see PointerInputModule.ShouldStartDrag
        /// </summary>
        /// <param name="pressPos"></param>
        /// <param name="currentPos"></param>
        /// <param name="threshold"></param>
        /// <param name="useDragThreshold"></param>
        /// <returns></returns>
        protected bool ShouldStartDrag(Vector2 pressPos, Vector2 currentPos, float threshold, bool useDragThreshold)
        {
            //Debug.Log($"pressPos:{pressPos}  currentPos:{currentPos}");

            if (!useDragThreshold)
                return true;

            return (pressPos - currentPos).sqrMagnitude >= threshold * threshold;
        }

        protected virtual void Drag(VRTK_UIPointer pointer, List<RaycastResult> results)
        {
            if (pointer.IsSelectionButtonPressed())
            {
                if (!pointer.pointerEventData.dragging && pointer.pointerEventData.delta != Vector2.zero)
                {
                    pointer.pointerEventData.dragging = ShouldStartDrag(
                        pointer.pointerEventData.pressPosition,
                        pointer.pointerEventData.position,
                        eventSystem.pixelDragThreshold,
                        pointer.pointerEventData.useDragThreshold
                        );

                    //if (pointer.pointerEventData.dragging)
                    //    Debug.Log("开启拖动-----");
                }
            }
            else
            {
                pointer.pointerEventData.dragging = false;
            }

            if (pointer.pointerEventData.pointerDrag)
            {
                if (!ValidElement(pointer.pointerEventData.pointerDrag))
                {
                    pointer.pointerEventData.pointerDrag = null;
                    return;
                }

                if (pointer.pointerEventData.dragging)
                {
                    if (IsHovering(pointer))
                    {
                        ExecuteEvents.ExecuteHierarchy(pointer.pointerEventData.pointerDrag, pointer.pointerEventData, ExecuteEvents.dragHandler);
                    }
                }
                else
                {
                    ExecuteEvents.ExecuteHierarchy(pointer.pointerEventData.pointerDrag, pointer.pointerEventData, ExecuteEvents.dragHandler);
                    ExecuteEvents.ExecuteHierarchy(pointer.pointerEventData.pointerDrag, pointer.pointerEventData, ExecuteEvents.endDragHandler);
                    for (int i = 0; i < results.Count; i++)
                    {
                        ExecuteEvents.ExecuteHierarchy(results[i].gameObject, pointer.pointerEventData, ExecuteEvents.dropHandler);
                    }
                    pointer.pointerEventData.pointerDrag = null;
                }
            }
            else if (pointer.pointerEventData.dragging)
            {
                for (int i = 0; i < results.Count; i++)
                {
                    RaycastResult result = results[i];
                    if (!ValidElement(result.gameObject))
                    {
                        continue;
                    }

                    ExecuteEvents.ExecuteHierarchy(result.gameObject, pointer.pointerEventData, ExecuteEvents.initializePotentialDrag);
                    ExecuteEvents.ExecuteHierarchy(result.gameObject, pointer.pointerEventData, ExecuteEvents.beginDragHandler);
                    GameObject target = ExecuteEvents.ExecuteHierarchy(result.gameObject, pointer.pointerEventData, ExecuteEvents.dragHandler);

                    if (target != null)
                    {
                        pointer.pointerEventData.pointerDrag = target;

                        //Debug.Log("set drag:" + pointer.pointerEventData.pointerDrag);
                        break;
                    }
                }

                #region fix
                //if (
                //    pointer.pointerEventData.pointerDrag != null
                //    &&
                //    pointer.pointerEventData.pointerPress != pointer.pointerEventData.pointerDrag 
                //    &&
                //    pointer.pointerEventData.pointerPress.transform.IsChildOf(pointer.pointerEventData.pointerDrag.transform)
                //    )
                //{
                //    Debug.Log("cancel pointer press:" + pointer.pointerEventData.pointerDrag);

                //    ExecuteEvents.ExecuteHierarchy(pointer.pointerEventData.pointerPress, pointer.pointerEventData, ExecuteEvents.pointerUpHandler);

                //    pointer.pointerEventData.eligibleForClick = false;

                //    pointer.pointerEventData.pointerPress = null;

                //    pointer.pointerEventData.rawPointerPress = null;
                //}

                #endregion
            }
        }

        //protected virtual void Drag(VRTK_UIPointer pointer, List<RaycastResult> results)
        //{
        //    pointer.pointerEventData.dragging = pointer.IsSelectionButtonPressed() && pointer.pointerEventData.delta != Vector2.zero;

        //    if (pointer.pointerEventData.pointerDrag)
        //    {
        //        if (!ValidElement(pointer.pointerEventData.pointerDrag))
        //        {
        //            pointer.pointerEventData.pointerDrag = null;
        //            return;
        //        }

        //        if (pointer.pointerEventData.dragging)
        //        {
        //            if (IsHovering(pointer))
        //            {
        //                ExecuteEvents.ExecuteHierarchy(pointer.pointerEventData.pointerDrag, pointer.pointerEventData, ExecuteEvents.dragHandler);
        //            }
        //        }
        //        else
        //        {
        //            ExecuteEvents.ExecuteHierarchy(pointer.pointerEventData.pointerDrag, pointer.pointerEventData, ExecuteEvents.dragHandler);
        //            ExecuteEvents.ExecuteHierarchy(pointer.pointerEventData.pointerDrag, pointer.pointerEventData, ExecuteEvents.endDragHandler);
        //            for (int i = 0; i < results.Count; i++)
        //            {
        //                ExecuteEvents.ExecuteHierarchy(results[i].gameObject, pointer.pointerEventData, ExecuteEvents.dropHandler);
        //            }
        //            pointer.pointerEventData.pointerDrag = null;
        //        }
        //    }
        //    else if (pointer.pointerEventData.dragging)
        //    {
        //        for (int i = 0; i < results.Count; i++)
        //        {
        //            RaycastResult result = results[i];
        //            if (!ValidElement(result.gameObject))
        //            {
        //                continue;
        //            }

        //            ExecuteEvents.ExecuteHierarchy(result.gameObject, pointer.pointerEventData, ExecuteEvents.initializePotentialDrag);
        //            ExecuteEvents.ExecuteHierarchy(result.gameObject, pointer.pointerEventData, ExecuteEvents.beginDragHandler);
        //            GameObject target = ExecuteEvents.ExecuteHierarchy(result.gameObject, pointer.pointerEventData, ExecuteEvents.dragHandler);

        //            Debug.Log("dragHandler:" + target);

        //            if (target != null)
        //            {
        //                pointer.pointerEventData.pointerDrag = target;
        //                break;
        //            }
        //        }
        //    }
        //}

        //使用touchpad 触摸来模拟鼠标滚轮。 适配模拟的手指触摸，屏蔽此功能。
        protected virtual void Scroll(VRTK_UIPointer pointer, List<RaycastResult> results)
        {
            //pointer.pointerEventData.scrollDelta = (pointer.controllerEvents != null ? pointer.controllerEvents.GetTouchpadAxis() : Vector2.zero);
            //bool scrollWheelVisible = false;
            //for (int i = 0; i < results.Count; i++)
            //{
            //    if (pointer.pointerEventData.scrollDelta != Vector2.zero)
            //    {
            //        GameObject target = ExecuteEvents.ExecuteHierarchy(results[i].gameObject, pointer.pointerEventData, ExecuteEvents.scrollHandler);
            //        if (target != null)
            //        {
            //            scrollWheelVisible = true;
            //        }
            //    }
            //}

            //if (pointer.controllerRenderModel != null)
            //{
            //    VRTK_SDK_Bridge.SetControllerRenderModelWheel(pointer.controllerRenderModel, scrollWheelVisible);
            //}
        }
    }
}