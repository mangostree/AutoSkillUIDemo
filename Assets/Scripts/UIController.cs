using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

// 简单 UI 栈：切面板时隐藏并回收当前页、路径入栈；Pop 时出栈并回到池里复用。

public class UIController : MonoBehaviour
{
	UIPanelHandle lastedPanel = null;
	Stack<string> uiStack;
	static UIController _instance = null;
	public static UIController Instance
	{
		get { return _instance; }
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
		uiStack = new Stack<string>();
	}


	// 打开新面板：收起当前页并入栈，再从对象池创建/复用 path 对应面板。
	public void CreatePanel(string path)
	{
		if (lastedPanel!=null)
		{
			lastedPanel.gameobj.SetActive(false);
			uiStack.Push(lastedPanel.element.path);
			lastedPanel.Recycle();
		}

        UIPanelHandle newPanel = UIPanelPool.Instance.CreateUIPanel(path);
		if (newPanel != null)
		{
			lastedPanel = newPanel;
		}
		else
		{
			PopPanel(); // load new panel failed, fallback
        }
    }

	// 关闭顶层面板并出栈：若有上一级则重新从池里拉起该路径。
	public void PopPanel()
	{
		if(lastedPanel!=null)
		{
			lastedPanel.gameobj.SetActive(false);
			lastedPanel.Recycle();
			lastedPanel = null;
		}
		if(uiStack.Count > 0)
		{
			string path = uiStack.Pop();
			lastedPanel = UIPanelPool.Instance.CreateUIPanel(path);
		}
	}
}
