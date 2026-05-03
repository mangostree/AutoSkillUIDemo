using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.EventSystems.EventTrigger;

namespace GIGA.AutoRadialLayout.RadialMenu
{

	public class RadialLayoutMenu : MonoBehaviour
	{
		private const int LAYER_INDEX_VISIBILITY = 0;
		private const int LAYER_INDEX_EXPAND = 1;
		private const int LAYER_INDEX_ROTATE = 2;
		private const int LAYER_INDEX_FADE = 3;
		public const float TIMER_CLOSE_AFTER_NO_MOUSEOVER = 0.5f;

		public enum VisibilityState { Hidden, Visible }
		public enum StartingOpenState { Closed, Open }
		public enum ChildNodeVisibility { Always, OnMouseOver, OnClick }
		public enum VisibilityAnimations { Instant, Fade }
		public enum OpeningAnimations { Instant, Animated }
		public enum InnerNodeClickAction {Open,Hide,HideAndClose,DoNothing };

		[Header("Initialization")]
		public VisibilityState startingVisibility = VisibilityState.Visible;
		public StartingOpenState startingOpenState = StartingOpenState.Open;

		[Header("References")]
		public CanvasGroup canvasGroup;
		public RadialLayout layout;
		public Animator animator;
		public GameObject labelsRoot;
		private RectTransform rectTransform;
		private RectTransform canvasRect;

		[Header("Settings")]
		public ChildNodeVisibility childVisibility = ChildNodeVisibility.OnClick;
		public bool hideWhenClickingNodes = true;
		public bool closeWhenClickingNodes = true;
		public bool showLinksForChildNodes = true;
		public InnerNodeClickAction innerNodeAction = InnerNodeClickAction.Open;

		[Header("Animations")]
		public VisibilityAnimations visibilityAnimation;
		public OpeningAnimations openingAnimation,closingAnimation;
		public bool openAnimation_expand = true;
		public bool openAnimation_rotate = true;
		public bool openAnimation_fade = true;

		public float showAnimationSpeed = 1;
		public float openingAnimationSpeed = 1;
		[HideInInspector]
		public float expandProgress = 1;
		[HideInInspector]
		public float rotateProgress = 1;
		[HideInInspector]
		public float fadeProgress = 1;
		private float savedOpeningRadius;
		private float savedCircleSlice;

		// Getters
		public bool IsVisible { get { return this.animator.GetBool("Show"); } }
		public bool IsOpen { get { return this.animator.GetBool("Open"); } }
		public bool IsFading { get { return this.animator.GetCurrentAnimatorStateInfo(LAYER_INDEX_VISIBILITY).IsTag("Animation"); } }
		public bool IsAnimating { get { return this.animator.GetCurrentAnimatorStateInfo(LAYER_INDEX_EXPAND).IsTag("Animation") || this.animator.GetCurrentAnimatorStateInfo(LAYER_INDEX_ROTATE).IsTag("Animation") || this.animator.GetCurrentAnimatorStateInfo(LAYER_INDEX_FADE).IsTag("Animation"); } }

		// Status
		public VisibilityState CurrentVisibilityState { get; private set; }
		private RadialLayoutMenuNode mouseOverNode;					// Reference to the last node that received mouse over event

		// Events
		public Action<bool> onOpenStatusChangeStart;                // Called when opening status change is called (starting of animation, or instant if not animated). Returns true if target state is Open
		public Action<bool> onOpenStatusChangeEnd;                  // Called when opening status change is ended (ending of animation, or instant if not animated). Returns true if target state is Open
		public Action<bool> onVisibilityStatusChangeStart;          // Called when visibility status change is called (starting of animation, or instant if not animated). Returns true if target state is Open

		// Flags & Timers
		private bool eventsRegistered;
		public bool Initialized { get; private set; }
		private float timer_NoMouseOver;

		// Misc
		public Transform AnchoredTransform { get; set; }

		private IEnumerator Start()
		{
			// Setting starting visibility & state
			this.savedOpeningRadius = this.layout.circleRadius;
			this.ChangeVisibilityStatus(this.startingVisibility == VisibilityState.Visible);
			this.animator.SetBool("Open", this.startingOpenState == StartingOpenState.Open ? true : false);

			// Finding references
			this.rectTransform = this.GetComponent<RectTransform>();
			this.canvasRect = this.GetComponentInParent<Canvas>()?.GetComponent<RectTransform>();

			// Setting min node scale to 0, so nodes can disappear when closed
			this.layout.nodeScale_min = 0;

			// Waiting first rebuild
			while (this.layout.Nodes == null)
				yield return null;

			// Saving target circle slice for rotation animation
			if (this.layout.circleSlice != 360)
				this.savedCircleSlice = this.layout.circleSlice;
			else
			{
				int firstNodesCount = this.layout.CountNodesOfDepth(0);
				this.savedCircleSlice = 360 / (float)firstNodesCount * (firstNodesCount - 1);
			}

			this.ChangeOpenStatus(this.startingOpenState == StartingOpenState.Open ? true : false);

			// Registering to layout rebuild event
			this.layout.onRebuild += this.OnLayoutRebuild;
			this.eventsRegistered = true;
			this.Initialized = true;
		}

		private void OnEnable()
		{
			if (!this.eventsRegistered && this.layout != null)
			{
				this.layout.onRebuild += this.OnLayoutRebuild;
				this.eventsRegistered = true;
			}
		}

		private void OnDisable()
		{
			if (this.eventsRegistered)
			{
				this.layout.onRebuild -= this.OnLayoutRebuild;
				this.eventsRegistered = false;
			}
		}

		private void OnLayoutRebuild(RadialLayout layout)
		{
			// Adding canvas group to link roots
			foreach (var linksRoot in this.GetComponentsInChildren<RadialLayoutLinksRoot>())
			{
				if(linksRoot.GetComponent<CanvasGroup>() == null)
					linksRoot.gameObject.AddComponent<CanvasGroup>();
				foreach(var link in linksRoot.GetComponentsInChildren<RadialLayoutLink>())
				if (link.GetComponent<CanvasGroup>() == null)
					link.gameObject.AddComponent<CanvasGroup>();
			}

			// Placing inner node & labels root on top
			this.layout.innerNode.transform.SetAsLastSibling();
			this.labelsRoot.transform.SetAsLastSibling();
		}

		private void Update()
		{
			if (Application.isPlaying && this.Initialized)
			{
				this.layout.circleRadius = this.expandProgress * this.savedOpeningRadius;
				this.layout.circleSlice = this.rotateProgress * this.savedCircleSlice;

				if (this.mouseOverNode == null && this.timer_NoMouseOver >= 0)
					this.timer_NoMouseOver += Time.deltaTime;

				// Closing all children if timed out && mode is OnMouseOver
				if (this.childVisibility == ChildNodeVisibility.OnMouseOver && this.timer_NoMouseOver >= TIMER_CLOSE_AFTER_NO_MOUSEOVER)
				{
					this.timer_NoMouseOver = -1;
					foreach (var node in this.layout.Nodes)
					{
						if (node.depth == 0)
							node.GetComponent<RadialLayoutMenuNode>().ShowChildren(false);
					}
				}

				// Keep position to anchoredObject
				if (this.AnchoredTransform != null && this.IsVisible)
					this.AnchorWorldPosition(this.AnchoredTransform.transform.position);

			}
		}

		#region Visibility Handling

		/// <summary>
		/// Shows the menu.
		/// </summary>
		public void Show()
		{
			this.ChangeVisibilityStatus(true);
		}

		/// <summary>
		/// Shows the menu in open state.
		/// </summary>
		public void ShowAndOpen()
		{
			this.ChangeVisibilityStatus(true);
			this.ChangeOpenStatus(true);
		}

		/// <summary>
		/// Shows the menu over the specified gameobject.
		/// </summary>
		/// <param name="targetObject"></param>
		/// <param name="keepAnchored">If true, the menu will stay anchored to the gameobject and will follow it as it moves.</param>
		public void Show(GameObject targetObject,bool keepAnchored = false)
		{
			this.AnchorWorldPosition(targetObject.transform.position);
			if (keepAnchored)
				this.AnchoredTransform = targetObject.transform;
			this.Show();
		}

		/// <summary>
		/// Shows the menu over the specified gameobject in open state.
		/// </summary>
		/// <param name="targetObject"></param>
		/// <param name="keepAnchored">If true, the menu will stay anchored to the gameobject and will follow it as it moves.</param>
		public void ShowAndOpen(GameObject targetObject, bool keepAnchored = false)
		{
			this.AnchorWorldPosition(targetObject.transform.position);
			if (keepAnchored)
				this.AnchoredTransform = targetObject.transform;
			this.ShowAndOpen();
		}

		/// <summary>
		/// Hides the menu.
		/// </summary>
		public void Hide()
		{
			this.ChangeVisibilityStatus(false);
		}

		/// <summary>
		/// Hides the menu and set it closed.
		/// </summary>
		public void HideAndClose()
		{
			this.ChangeVisibilityStatus(false);
			this.ChangeOpenStatus(false);
		}

		private void ChangeVisibilityStatus(bool show)
		{
			this.animator.SetBool("Show", show);
			this.animator.SetBool("AnimatedShow", this.visibilityAnimation != VisibilityAnimations.Instant);
			this.animator.SetFloat("ShowSpeed", this.showAnimationSpeed);
			this.CurrentVisibilityState = show ? VisibilityState.Visible : VisibilityState.Hidden;
			if(this.Initialized)
				this.onVisibilityStatusChangeStart?.Invoke(show);

			this.StopCoroutine("ChangeVisibilityStatusCoroutine");
			this.StartCoroutine("ChangeVisibilityStatusCoroutine");
		}

		private IEnumerator ChangeVisibilityStatusCoroutine()
		{
			// Waiting for animation to begin
			float elapsed = 0;
			if (this.animator.GetBool("AnimatedShow"))
			{
				while (!this.IsFading && elapsed < 0.2f)
				{
					elapsed += Time.deltaTime;
					yield return null;
				}
			}
			else
				yield return null;

			// Waiting for animation to end
			while (this.IsFading)
			{
				float linksAlpha = this.canvasGroup.alpha;

				if (this.animator.GetBool("Show")) // opening
				{
					linksAlpha = Mathf.Pow(this.canvasGroup.alpha, 2);
					if (this.openingAnimation == OpeningAnimations.Animated && this.openAnimation_fade)
						linksAlpha = Mathf.Min(this.fadeProgress, linksAlpha);
				}
				else
				{
					linksAlpha = this.canvasGroup.alpha * 0.75f;
					if (this.closingAnimation == OpeningAnimations.Animated && this.openAnimation_fade)
						linksAlpha = Mathf.Min(this.fadeProgress, linksAlpha);
				}

				if (!this.openAnimation_fade || this.animator.GetBool("Show") && this.openingAnimation == OpeningAnimations.Instant || !this.animator.GetBool("Show") && this.closingAnimation == OpeningAnimations.Instant)
				{
					foreach (var node in this.layout.Nodes)
					{
						if (node.depth == 0 || this.childVisibility == ChildNodeVisibility.Always)
						{
							var canvasGroup = node.GetComponent<CanvasGroup>();
							if (canvasGroup != null)
								canvasGroup.alpha = this.canvasGroup.alpha;
						}
					}
				}

				foreach (var linksRoot in this.GetComponentsInChildren<RadialLayoutLinksRoot>())
				{
					var linksCanvasGroup = linksRoot.GetComponent<CanvasGroup>();
					if (linksCanvasGroup != null)
						linksCanvasGroup.alpha = this.canvasGroup.alpha;
				}
				yield return null;
			}

			yield return null;

			if (!this.openAnimation_fade)
			{
				foreach (var node in this.layout.Nodes)
				{
					if (node.depth == 0 || this.childVisibility == ChildNodeVisibility.Always)
					{
						var canvasGroup = node.GetComponent<CanvasGroup>();
						if (canvasGroup != null)
							canvasGroup.alpha = this.fadeProgress;
					}
				}

				foreach (var linksRoot in this.GetComponentsInChildren<RadialLayoutLinksRoot>())
				{
					var linksCanvasGroup = linksRoot.GetComponent<CanvasGroup>();

					if (linksCanvasGroup != null)
						linksCanvasGroup.alpha = canvasGroup.alpha;
				}
			}
			
		}

		/// <summary>
		/// Opens the menu.
		/// </summary>
		public void Open()
		{
			this.ChangeOpenStatus(true);
		}

		/// <summary>
		/// Closes the menu.
		/// </summary>
		public void Close()
		{
			this.ChangeOpenStatus(false);
		}

		private void ChangeOpenStatus(bool open)
		{
			this.animator.SetBool("Open", open);
			this.animator.SetBool("AnimatedOpen", this.openingAnimation == OpeningAnimations.Animated);
			this.animator.SetBool("AnimatedClose", this.closingAnimation == OpeningAnimations.Animated);
			this.animator.SetBool("Animation_Expand", this.openAnimation_expand);
			this.animator.SetBool("Animation_Rotate", this.openAnimation_rotate);
			this.animator.SetBool("Animation_Fade", this.openAnimation_fade);

			this.animator.SetFloat("OpenSpeed", this.openingAnimationSpeed);

			// Setting animation layers weight
			this.animator.SetLayerWeight(LAYER_INDEX_ROTATE, this.openingAnimation == OpeningAnimations.Animated && open || this.closingAnimation == OpeningAnimations.Animated && !open ? 1 : 0); // Rotating
			this.animator.SetLayerWeight(LAYER_INDEX_FADE, this.openingAnimation == OpeningAnimations.Animated && open || this.closingAnimation == OpeningAnimations.Animated && !open ? 1 : 0); // Fading

			// Disabling / Enabling interaction with nodes
			foreach (var node in this.layout.Nodes)
				node.GetComponent<CanvasGroup>().blocksRaycasts = false;

			this.StopCoroutine("ChangeOpenStatusCoroutine");
			this.StartCoroutine("ChangeOpenStatusCoroutine");

			this.onOpenStatusChangeStart?.Invoke(open);

		}

		private IEnumerator ChangeOpenStatusCoroutine()
		{
			// Waiting for animation to begin
			if (this.Initialized && this.IsVisible) // Skipping animations on setup
			{
				float elapsed = 0;
				if (this.animator.GetBool("AnimatedOpen"))
				{
					while (!this.IsAnimating && elapsed < 0.2f)
					{
						elapsed += Time.deltaTime;
						yield return null;
					}
				}
				else
					yield return null;
			}

			// Waiting for animation to end
			while (this.IsAnimating)
			{
				if (this.openAnimation_fade)
				{
					foreach (var node in this.layout.Nodes)
					{
						if (node.depth == 0 || this.childVisibility == ChildNodeVisibility.Always)
						{
							var canvasGroup = node.GetComponent<CanvasGroup>();
							if (canvasGroup != null)
								canvasGroup.alpha = this.fadeProgress;
						}
					}

					foreach (var linksRoot in this.GetComponentsInChildren<RadialLayoutLinksRoot>())
					{
						var linksCanvasGroup = linksRoot.GetComponent<CanvasGroup>();

						if (linksCanvasGroup != null)
						{
							if (!this.IsFading)
							{
								linksCanvasGroup.alpha = this.fadeProgress;
							}
						}
					}
				}
				yield return null;
			}

			if(this.Initialized)
				yield return null;

			//  Enabling interaction with nodes if open
			foreach (var node in this.layout.Nodes)
			{
				var canvasGroup = node.GetComponent<CanvasGroup>();
				if (canvasGroup != null)
				{
					if (this.expandProgress == 1 && this.rotateProgress == 1)
						canvasGroup.blocksRaycasts = true;
					canvasGroup.alpha = this.fadeProgress;
				}
			}

			foreach (var linksRoot in this.GetComponentsInChildren<RadialLayoutLinksRoot>())
			{
				var linksCanvasGroup = linksRoot.GetComponent<CanvasGroup>();

				if (linksCanvasGroup != null)
				{
					if (!this.IsFading)
					{
						linksCanvasGroup.alpha = this.fadeProgress;
					}
				}
			}

			this.onOpenStatusChangeEnd?.Invoke(this.IsOpen);
		}

		/// <summary>
		/// Sets the current node on which the mouse cursor is over. (Internal use only)
		/// </summary>
		/// <param name="node"></param>
		public void SetMouseOverNode(RadialLayoutMenuNode node)
		{
			if (node != null)
				this.timer_NoMouseOver = 0;
			this.mouseOverNode = node;
		}

		#endregion

		#region Click Listeners

		/// <summary>
		/// Called when clicking on Inner Node
		/// </summary>
		public void OnInnerNodeClick()
		{
			if (!this.IsAnimating)
			{
				switch (this.innerNodeAction)
				{
					case InnerNodeClickAction.Open:
						this.ChangeOpenStatus(!this.IsOpen);
						break;
					case InnerNodeClickAction.Hide:
						this.Hide();
						break;
					case InnerNodeClickAction.HideAndClose:
						this.HideAndClose();
						break;
				}
			}
		}

		/// <summary>
		/// Called when a Node is clicked
		/// </summary>
		public void OnNodeClick(RadialLayoutMenuNode callingNode)
		{
			// Handling closing/hiding on click
			if (this.hideWhenClickingNodes || this.closeWhenClickingNodes)
			{
				// If clicked node has children, ignore
				if (!callingNode.Node.HasChildren)
				{
					if (this.hideWhenClickingNodes)
						this.ChangeVisibilityStatus(false);
					if (this.closeWhenClickingNodes)
						this.ChangeOpenStatus(false);
				}
			}
		}

		#endregion

		#region Position Handlers

		/// <summary>
		/// Moves the menu to current mouse position.
		/// </summary>
		public void MoveToMousePosition()
		{
			if (Camera.main != null)
			{
				if (this.canvasRect.GetComponent<Canvas>().renderMode == RenderMode.ScreenSpaceOverlay)
				{
					rectTransform.position = Input.mousePosition;
				}
				else
				{
					RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, Input.mousePosition, Camera.main, out Vector2 localPoint);
					rectTransform.anchoredPosition = localPoint;
				}
			}
		}

		private void AnchorWorldPosition(Vector3 worldPosition)
		{
			if (Camera.main)
			{
				Vector2 viewPortPos = Camera.main.WorldToViewportPoint(worldPosition);

				Vector2 WorldObject_ScreenPosition = new Vector2(
				((viewPortPos.x * canvasRect.sizeDelta.x) - (canvasRect.sizeDelta.x * 0.5f)),
				((viewPortPos.y * canvasRect.sizeDelta.y) - (canvasRect.sizeDelta.y * 0.5f)));

				rectTransform.anchoredPosition = WorldObject_ScreenPosition;
			}
		}

		#endregion


	}
}
