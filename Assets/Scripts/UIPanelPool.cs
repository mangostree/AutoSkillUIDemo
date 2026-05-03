using System;
using System.Collections.Generic;
using UnityEngine;

// UI 面板对象池：按 prefab 路径复用实例，无引用时分帧延迟销毁以摊平开销。

public class UIPanelHandle
{
	// 句柄：只维护 refCount；归零时由池把节点从 used 列表移回无引用环（Demote）。
	public UIPanelInstanceElement element = null;
	bool recycled = false;

	public UIPanelHandle(UIPanelInstanceElement element)
	{
		this.element = element;
		this.element.refCount++;
		this.element.discardTime = 0;
		Debug.Log($"addref:{element.path}:{element.refCount}");
	}

	public GameObject gameobj
	{
		get
		{
			return element.obj;
		}
	}

	public void Recycle()
	{
		if (recycled == true)
		{
			return;
		}
		this.element.refCount--;
		Debug.Log($"subref:{element.path}:{this.element.refCount}");
		if (this.element.refCount == 0 && this.element.ownerPrefab != null)
		{
			this.element.ownerPrefab.DemoteUsedToUnreferenced(this.element);
		}
		recycled = true;
	}
	~UIPanelHandle()
	{
		Recycle();
	}
}

public class UIPanelInstanceElement
{
	// 单个实例节点；next/prev 仅当在「无引用环」上时有效。usedInstanceListIndex 仅在 used 列表中有效。
	public GameObject obj = null;
	public UIPanelInstanceElement next = null;
	public UIPanelInstanceElement prev = null;
	public int refCount = 0;
	public float discardTime = 0;
	public string path;
	public UIPrefabElement ownerPrefab = null;
	public int usedInstanceListIndex = -1;
}

public class UIPanelInstanceChain
{
	// 仅存放 refCount==0 的实例；双向环形链表，便于 Scan 按步推进、控制每帧工作量。

	public UIPanelInstanceElement current = null;
	private uint count = 0;

	public uint Count
	{
		get { return count; }
	}

	public void InsertUnreferenced(UIPanelInstanceElement element)
	{
		if (current == null)
		{
			current = element;
			element.next = element;
			element.prev = element;
		}
		else
		{
			current.prev.next = element;
			element.prev = current.prev;
			element.next = current;
			current.prev = element;
		}
		count++;
	}

	public UIPanelInstanceElement FindReusable()
	{
		if (current == null || count == 0)
		{
			return null;
		}

		UIPanelInstanceElement iter = current;
		for (int i = 0; i < count; i++)
		{
			if (iter != null && iter.obj != null)
			{
				current = iter;
				return iter;
			}
			iter = iter.next;
		}
		return null;
	}

	public void DeleteUnreferenced(UIPanelInstanceElement node)
	{
		if (node == null || current == null || count == 0)
		{
			return;
		}

		if (count == 1)
		{
			current.prev = null;
			current.next = null;
			current = null;
			count--;
		}
		else
		{
			UIPanelInstanceElement prev = node.prev;
			UIPanelInstanceElement next = node.next;

			prev.next = next;
			next.prev = prev;
			if (current == node)
			{
				current = next;
			}
			node.prev = null;
			node.next = null;
			count--;
		}
	}

	public void Scan(uint step)
	{
		// 每帧只处理 step 个节点：累加闲置时间，超时 Destroy 并从环上摘除。
		if (current == null)
		{
			return;
		}

		step = Math.Min(step, count);
		for (int i = 0; i < step; i++)
		{
			UIPanelInstanceElement next = current.next;
			if (current.obj == null)
			{
				DeleteUnreferenced(current);
				if (count == 0)
				{
					current = null;
					return;
				}
				current = next;
				continue;
			}

			current.discardTime += Time.deltaTime;
			if (current.discardTime > 5)
			{
				GameObject.Destroy(current.obj);
				Debug.Log($"destroy panel instance:{current.path}:{current.refCount}:{current.discardTime}");
				DeleteUnreferenced(current);
				if (count == 0)
				{
					current = null;
					return;
				}
				current = next;
				continue;
			}

			current = next;
		}
	}
}

public class UIPrefabElement
{
	// 一种 prefab：instanceChain=闲置可销毁环；usedInstance=当前有引用的实例（与末尾交换删除）。

	public string path;
	public GameObject prefab = null;
	public UIPanelInstanceChain instanceChain = new UIPanelInstanceChain();
	public List<UIPanelInstanceElement> usedInstance = new List<UIPanelInstanceElement>();
	public UIPrefabElement next = null;
	public UIPrefabElement prev = null;
	public float discardTime = 0;

	public uint InstanceCount
	{
		get { return instanceChain.Count + (uint)usedInstance.Count; }
	}

	public UIPanelHandle TryAcquireReusedHandle()
	{
		// 优先复用环上节点；否则 Instantiate 并直接加入 used（CreateUIPanel 入口委托此处）。
		UIPanelInstanceElement reuseElement = instanceChain.FindReusable();
		if (reuseElement != null)
		{
			reuseElement.obj.SetActive(true);
			NormalizeFullscreenStretchUiRoot(reuseElement.obj);
			PromoteUnreferencedToUsed(reuseElement);
			return new UIPanelHandle(reuseElement);
		}

		if (prefab == null)
		{
			return null;
		}

		UIPanelPool pool = UIPanelPool.Instance;
		if (pool == null)
		{
			return null;
		}

		GameObject targetPrefab = UnityEngine.Object.Instantiate(prefab, new Vector3(0, 0, 0), Quaternion.identity);
		// worldPositionStays=true 会强行保留世界坐标，常把全屏 stretch 的 RectTransform 算成「只占父级一半宽」等错位。
		targetPrefab.transform.SetParent(pool.transform, false);
		ApplyPoolPanelRootPosition(targetPrefab, pool);

		UIPanelInstanceElement element = new UIPanelInstanceElement();
		element.path = path;
		element.obj = targetPrefab;
		element.ownerPrefab = this;
		AddUsedInstance(element);

		return new UIPanelHandle(element);
	}

	/// <summary>
	/// 铺满父 Rect 的根 UI（anchor 0~1）若曾被写入 localPosition，子物体「贴父级右缘」会整体错位；复用时归零 anchoredPosition。
	/// </summary>
	static void NormalizeFullscreenStretchUiRoot(GameObject root)
	{
		RectTransform rt = root.GetComponent<RectTransform>();
		if (rt == null)
			return;
		if (rt.anchorMin == Vector2.zero && rt.anchorMax == Vector2.one)
			ResetFullscreenStretchToFillParent(rt);
	}

	/// <summary>
	/// 全屏拉伸 UI 保持 prefab 布局；非全屏面板仍按屏幕中心对齐（旧逻辑）。
	/// </summary>
	static void ApplyPoolPanelRootPosition(GameObject root, UIPanelPool pool)
	{
		RectTransform rt = root.GetComponent<RectTransform>();
		if (rt == null)
			return;
		if (rt.anchorMin == Vector2.zero && rt.anchorMax == Vector2.one)
		{
			ResetFullscreenStretchToFillParent(rt);
			return;
		}

		RectTransform poolRect = pool.GetComponent<RectTransform>();
		if (poolRect == null)
			return;

		Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
		RectTransformUtility.ScreenPointToLocalPointInRectangle(poolRect, screenCenter, null, out Vector2 localPosition);
		rt.localPosition = localPosition;
	}

	/// <summary>全屏四锚点拉伸时，用 offset 贴齐父 Rect，避免 SetParent/坐标切换后只剩半屏宽等问题。</summary>
	static void ResetFullscreenStretchToFillParent(RectTransform rt)
	{
		rt.offsetMin = Vector2.zero;
		rt.offsetMax = Vector2.zero;
		rt.anchoredPosition = Vector2.zero;
		rt.localScale = Vector3.one;
	}

	public void AddUsedInstance(UIPanelInstanceElement element)
	{
		usedInstance.Add(element);
		element.usedInstanceListIndex = usedInstance.Count - 1;
	}

	public void PromoteUnreferencedToUsed(UIPanelInstanceElement element)
	{
		// 复用领取：从环移入 used。
		instanceChain.DeleteUnreferenced(element);
		usedInstance.Add(element);
		element.usedInstanceListIndex = usedInstance.Count - 1;
	}

	public void DemoteUsedToUnreferenced(UIPanelInstanceElement element)
	{
		// 引用归零：从 used 换尾删除后挂回环，进入延迟销毁队列。
		int idx = element.usedInstanceListIndex;
		if (idx < 0 || idx >= usedInstance.Count)
		{
			return;
		}
		int last = usedInstance.Count - 1;
		if (idx != last)
		{
			UIPanelInstanceElement lastEl = usedInstance[last];
			usedInstance[idx] = lastEl;
			lastEl.usedInstanceListIndex = idx;
		}
		usedInstance.RemoveAt(last);
		element.usedInstanceListIndex = -1;
		instanceChain.InsertUnreferenced(element);
	}
}

public class UIPrefabChain
{
	public UIPrefabElement current = null;
	private uint count = 0;
	private Dictionary<string, UIPrefabElement> map = new Dictionary<string, UIPrefabElement>();

	public UIPrefabElement Find(string path)
	{
		if (map.ContainsKey(path))
		{
			return map[path];
		}
		return null;
	}

	public void Insert(UIPrefabElement element)
	{
		if (current == null)
		{
			current = element;
			element.next = element;
			element.prev = element;
		}
		else
		{
			current.prev.next = element;
			element.prev = current.prev;
			element.next = current;
			current.prev = element;
		}
		map.Add(element.path, element);
		count++;
	}

	public void Delete(UIPrefabElement node)
	{
		if (node == null || current == null || count == 0)
		{
			return;
		}

		map.Remove(node.path);
		if (count == 1)
		{
			current.prev = null;
			current.next = null;
			current = null;
			count--;
			return;
		}

		UIPrefabElement prev = node.prev;
		UIPrefabElement next = node.next;
		prev.next = next;
		next.prev = prev;
		if (current == node)
		{
			current = next;
		}
		node.prev = null;
		node.next = null;
		count--;
	}

	public void Scan(uint prefabStep, uint panelStepPerPrefab)
	{
		// 分帧：轮询若干 prefab，每个再扫若干无引用实例；全空 prefab 延迟卸引用。
		prefabStep = Math.Min(prefabStep, count);
		for (uint i = 0; i < prefabStep; i++)
		{
			if (current == null)
			{
				return;
			}

			UIPrefabElement nextPrefab = current.next;
			current.instanceChain.Scan(panelStepPerPrefab);

			if (current.InstanceCount == 0)
			{
				current.discardTime += Time.deltaTime;
				if (current.discardTime > 5)
				{
					// Prefab 不能走 UnloadAsset；置空引用，实际释放依赖后续 UnloadUnusedAssets。
					current.prefab = null;
					Debug.Log($"unload prefab:{current.path}");
					Delete(current);
					current = nextPrefab;
					continue;
				}
			}
			else
			{
				current.discardTime = 0;
			}

			current = nextPrefab;
		}
	}
}

public class UIPanelPool : MonoBehaviour
{
	// 单例；场景内需挂一个实例，Awake 写入 Instance。

	UIPrefabChain prefabChain = new UIPrefabChain();
	static UIPanelPool _instance = null;
	public static UIPanelPool Instance
	{
		get { return _instance; }
	}

	public uint prefabStep = 2;
	public uint panelStepPerPrefab = 3;

	// 按 path 取/建 UIPrefabElement，再 TryAcquire（复用或新建）。
	public UIPanelHandle CreateUIPanel(string path)
	{
		UIPrefabElement prefabElement = prefabChain.Find(path);
		if (prefabElement == null)
		{
			GameObject loadedPrefab = Resources.Load<GameObject>(path);
			if (loadedPrefab == null)
			{
				Debug.LogError("path is wrong");
				return null;
			}

			prefabElement = new UIPrefabElement();
			prefabElement.path = path;
			prefabElement.prefab = loadedPrefab;
			prefabChain.Insert(prefabElement);
		}

		return prefabElement.TryAcquireReusedHandle();
	}

	private void Awake()
	{
		if (_instance == null)
		{
			_instance = this;
		}
		else
		{
			Debug.LogError("multiple instance set action of PlayerController");
		}
	}
	void Start()
	{
	}

	// 每帧驱动分帧销毁与空 prefab 卸载。
	void Update()
	{
		prefabChain.Scan(prefabStep, panelStepPerPrefab);
	}
}
