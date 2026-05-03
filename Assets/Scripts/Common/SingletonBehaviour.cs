using UnityEngine;

/// <summary>
/// 供 <see cref="MonoBehaviour"/> 子类继承的单例基类。<br/>
/// 在 <see cref="Awake"/> 中登记 <see cref="Instance"/>，不会在访问 <see cref="Instance"/> 时再查找或创建物体。<br/>
/// 子类写法：<c>public class Foo : SingletonBehaviour&lt;Foo&gt; { }</c>；场景中需存在挂有该组件的物体并处于启用状态。
/// </summary>
[DefaultExecutionOrder(-1000)]
public abstract class SingletonBehaviour<T> : MonoBehaviour where T : SingletonBehaviour<T>
{
	static T s_instance;

	/// <summary>跨场景保留（仅首个实例生效）。</summary>
	protected virtual bool UseDontDestroyOnLoad => false;

	/// <summary>仅在对应物体的 <see cref="Awake"/> 执行后可用；此前为 null。</summary>
	public static T Instance { get { return s_instance; } }

	protected virtual void Awake()
	{
		if (s_instance != null && s_instance != this)
		{
			Destroy(gameObject);
			return;
		}

		s_instance = (T)this;
		if (UseDontDestroyOnLoad)
			DontDestroyOnLoad(gameObject);
	}

	protected virtual void OnDestroy()
	{
		if (s_instance == this)
			s_instance = null;
	}
}
