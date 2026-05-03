using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GIGA.AutoRadialLayout.RadialMenu
{
	[ExecuteInEditMode]
	public class RadialLayoutMenuNode : MonoBehaviour
	{
		public enum LabelVisibility {Always,OnMouseOver,Never }

		public RadialLayoutMenu ParentMenu { get; private set; }
		public RadialLayoutNode Node { get; private set; }

		private RadialLayoutLink link;

		[Header("References")]
		public Transform labelContainer;
		public Text label;
		public CanvasGroup canvasGroup;

		[Header("Settings")]
		public LabelVisibility labelVisibility = LabelVisibility.OnMouseOver;
		public bool showLabelIfChildrenAreOpen = true;
		public int labelFontSize = 24;
		
		private int referenceFontSize;
		private float lastScalingFactor;

		// Flags
		public bool Hidden { get; private set; }
		private bool childrenShown;

		private void OnEnable()
		{
			if(this.ParentMenu == null)
				this.ParentMenu = this.GetComponentInParent<RadialLayoutMenu>();
			if (this.ParentMenu != null)
			{
				this.ParentMenu.onOpenStatusChangeStart += this.OnMenuOpenStatusChangeStart;
				this.ParentMenu.onOpenStatusChangeEnd += this.OnMenuOpenStatusChangeEnd;
				this.ParentMenu.onVisibilityStatusChangeStart += this.OnMenuVisibilityStatusChangeStart;
			}
		}

		private void OnDisable()
		{
			if (this.ParentMenu != null)
			{
				this.ParentMenu.onOpenStatusChangeStart -= this.OnMenuOpenStatusChangeStart;
				this.ParentMenu.onOpenStatusChangeEnd -= this.OnMenuOpenStatusChangeEnd;
				this.ParentMenu.onVisibilityStatusChangeStart -= this.OnMenuVisibilityStatusChangeStart;
			}
		}

		private IEnumerator Start()
		{
			if (Application.isPlaying)
			{
				this.Node = this.GetComponent<RadialLayoutNode>();
				this.canvasGroup = this.GetComponent<CanvasGroup>();
				if (this.labelVisibility != LabelVisibility.Always)
					this.label.gameObject.SetActive(false);

				// Waiting menu initialization
				while (!this.ParentMenu.Initialized)
					yield return null;

				// Hiding child nodes
				if (this.ParentMenu.childVisibility != RadialLayoutMenu.ChildNodeVisibility.Always)
				{
					this.ShowChildren(false);
					this.childrenShown = true;
				}
				else
					this.childrenShown = false;
			}
			
			this.referenceFontSize = this.labelFontSize;
			this.lastScalingFactor = this.transform.localScale.x;
			
			
		}

		private void Update()
		{
			if (Application.isPlaying)
			{
				if (this.labelFontSize != this.referenceFontSize)
					this.referenceFontSize = this.labelFontSize;

				// Rescaling font size
				float scalingFactor = this.transform.localScale.x;
				if (scalingFactor != this.lastScalingFactor)
				{
					this.label.transform.localScale = scalingFactor != 0 ? Vector3.one / scalingFactor : Vector3.zero;
					this.label.fontSize = (int)(this.referenceFontSize * scalingFactor);
					this.lastScalingFactor = scalingFactor;
				}
			}
		}

		private void OnMenuOpenStatusChangeStart(bool open)
		{
			// Handling labels
			if (this.labelVisibility != LabelVisibility.Always || !open)
				this.ShowLabel(false);

			// Handling children
			if(this.ParentMenu.childVisibility != RadialLayoutMenu.ChildNodeVisibility.Always)
				this.ShowChildren(false);

		}

		private void OnMenuOpenStatusChangeEnd(bool open)
		{
			// Handling labels
			if (this.labelVisibility == LabelVisibility.Always && open)
				this.ShowLabel(true);
			else
				this.ShowLabel(false);

			// Handling children
			if (this.ParentMenu.childVisibility != RadialLayoutMenu.ChildNodeVisibility.Always)
				this.ShowChildren(false);
			else
				this.ShowChildren(open);
		}

		private void OnMenuVisibilityStatusChangeStart(bool open)
		{
			// Handling children
			if (this.ParentMenu.childVisibility != RadialLayoutMenu.ChildNodeVisibility.Always)
				this.ShowChildren(false);
		}

		#region Mouse Listeners

		/// <summary>
		/// Called when this node is clicked
		/// </summary>
		public void OnClick()
		{
			if (Application.isPlaying)
			{
				// Communicating click to menu
				this.ParentMenu.OnNodeClick(this);

				// Opening child nodes
				if (this.ParentMenu.childVisibility == RadialLayoutMenu.ChildNodeVisibility.OnClick && this.Node.HasChildren)
				{
					// using first child to determine status
					foreach (Transform t in this.Node.transform)
					{
						var n = t.GetComponent<RadialLayoutNode>();
						if (n != null)
						{
							this.ShowChildren(n.GetComponent<RadialLayoutMenuNode>().Hidden);
							break;
						}
					}
				}
			}
		}

		/// <summary>
		/// Called on mouse enter
		/// </summary>
		public void OnPointerEnter(BaseEventData eventData)
		{
			if (Application.isPlaying)
			{
				if (this.labelVisibility == LabelVisibility.OnMouseOver && !this.ParentMenu.IsAnimating)
					this.ShowLabel(true);

				// Link animation
				if (this.Node.ArrivingLink != null)
					this.Node.ArrivingLink.ProgressValue = 1;

				// Child nodes
				if (this.ParentMenu.childVisibility == RadialLayoutMenu.ChildNodeVisibility.OnMouseOver)
					this.ShowChildren(true);

				this.ParentMenu.SetMouseOverNode(this);
			}
		}

		/// <summary>
		/// Called on mouse exit
		/// </summary>
		public void OnPointerExit(BaseEventData eventData)
		{
			if (Application.isPlaying)
			{
				if (this.labelVisibility == LabelVisibility.OnMouseOver)
					this.ShowLabel(false);

				// Link animation
				if (this.Node.ArrivingLink != null)
					this.Node.ArrivingLink.ProgressValue = 0;

				this.ParentMenu.SetMouseOverNode(null);
			}
		}

		#endregion

		#region Label visibility
		
		/// <summary>
		/// Shows / Hides the label of the node.
		/// </summary>
		public void ShowLabel(bool show)
		{
			// Checking child node condition
			bool childrenCheck = !this.Node.HasChildren || !this.childrenShown || this.showLabelIfChildrenAreOpen;

			this.label.gameObject.SetActive(show && childrenCheck);

			// Bringing label to front
			if (show && childrenCheck)
				this.label.transform.SetParent(this.ParentMenu.labelsRoot.transform);
			else
				this.label.transform.SetParent(this.labelContainer);
		}

		#endregion

		#region Children visibility handling

		/// <summary>
		/// Shows / Hides the child nodes of this node.
		/// </summary>
		public void ShowChildren(bool show)
		{
			if (Application.isPlaying)
			{
				foreach (var child in this.Node.GetChildNodes())
				{
					child.GetComponent<CanvasGroup>().alpha = show ? 1 : 0;
					child.GetComponent<CanvasGroup>().blocksRaycasts = show;
					child.GetComponent<RadialLayoutMenuNode>().Hidden = !show;

					if (this.Node.DepartingLinks != null)
						foreach (var link in this.Node.DepartingLinks)
							link.gameObject.SetActive(show && this.ParentMenu.showLinksForChildNodes);

					// Hiding recursively
					if(!show && child.GetComponent<RadialLayoutMenuNode>() != null)
						child.GetComponent<RadialLayoutMenuNode>().ShowChildren(false);
				}

				// Hiding children of siblings when opening a new set of children
				if (show && this.ParentMenu.childVisibility != RadialLayoutMenu.ChildNodeVisibility.Always)
				{
					if (this.Node.ParentNode != null)
					{
						foreach (var sibling in this.Node.ParentNode.GetChildNodes())
						{
							if (sibling != this.Node && sibling.depth == this.Node.depth)
							{
								RadialLayoutMenuNode menuNode = sibling.GetComponent<RadialLayoutMenuNode>();
								if (menuNode != null)
									menuNode.ShowChildren(false);
							}
						}
					}
					else if (this.Node.Layout != null)
					{
						foreach (var sibling in this.Node.Layout.Nodes)
						{
							if (sibling.depth == 0 && sibling != this.Node && sibling.depth == this.Node.depth)
							{
								RadialLayoutMenuNode menuNode = sibling.GetComponent<RadialLayoutMenuNode>();
								if (menuNode != null)
									menuNode.ShowChildren(false);
							}
						}
					}
				}

				this.childrenShown = show;

				if (this.labelVisibility != LabelVisibility.Always && show && !this.showLabelIfChildrenAreOpen)
					this.ShowLabel(false);

			}
		}

		#endregion
	}
}
