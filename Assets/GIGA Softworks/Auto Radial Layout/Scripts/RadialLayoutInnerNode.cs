using System.Collections;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace GIGA.AutoRadialLayout
{

    [ExecuteInEditMode]
    public class RadialLayoutInnerNode : MonoBehaviour
    {
        public RadialLayout layout;
        public float nodeRadius;

        // Flags
        public bool showNodeRadiusGizmo;

#if UNITY_EDITOR
    void Update()
        {
            // Forced nodes selection
            if (this.layout != null && this.layout.GetMasterLayout().forceNodesSelection && Selection.activeGameObject != null && (Selection.activeGameObject == this.gameObject || Selection.activeGameObject.transform.IsChildOf(this.transform)) && Selection.activeGameObject.GetComponent<RadialLayout>() == null)
            {
                GameObject target = this.layout.gameObject;

                if ((RadialLayout.forceSelectionTargetedObject == null || RadialLayout.forceSelectionTargetedObject != target) && (RadialLayout.forceSelectionSkippedObject == null || RadialLayout.forceSelectionSkippedObject != Selection.activeGameObject))
                {
                    if (Selection.activeGameObject.GetComponent<RadialLayout>() == null)
                        RadialLayout.forceSelectionSkippedObject = Selection.activeGameObject;
                    Selection.activeGameObject = target;
                    RadialLayout.forceSelectionTargetedObject = target;
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            Color exColor = Gizmos.color;
            if (this.showNodeRadiusGizmo)
            {
                Gizmos.color = Color.gray;
                float canvasScale = 1;

                if (this.layout != null)
                    canvasScale = !this.layout.IsNestedCanvas ? this.layout.Canvas.transform.localScale.x : this.layout.Canvas.transform.lossyScale.x;
                else
                    canvasScale = this.GetComponentInParent<Canvas>().transform.lossyScale.x;

				Gizmos.DrawWireSphere(this.transform.position, this.nodeRadius * this.transform.localScale.x * canvasScale);
            }
            Gizmos.color = exColor;
        }
#endif

    }
}
