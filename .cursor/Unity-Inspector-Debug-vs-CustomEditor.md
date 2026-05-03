# Unity Inspector：Debug 模式与 CustomEditor 的关系

## 结论（简要）

在 Inspector 使用 **Debug**（或 **Debug Internal**）视图时，Unity **不会调用** 针对该组件注册的 **`CustomEditor` 的 `OnInspectorGUI()`**。界面改为由编辑器内置的 **Debug 绘制路径** 负责：按序列化字段树展示属性。

---

## 具体说明

| 模式 | 行为 |
|------|------|
| **Normal** | 若存在 `[CustomEditor(typeof(T))]`，则绘制该类的 `OnInspectorGUI()`（自定义按钮、布局、隐藏字段等）。 |
| **Debug** | **不走** 上述 `OnInspectorGUI()`；改用通用 Debug Inspector，列出序列化成员（含未在自定义面板里画出的字段，如 `innerNode`、`linksRoot`）。 |

注意：

- **CustomEditor 类仍然存在于工程中并完成注册**；只是 **当前这次 Inspector 绘制** 不执行它的 `OnInspectorGUI()`，而不是「整个编辑器脚本被卸载」。
- 因此：**不是**「只禁用 `OnInspectorGUI` 的某一段逻辑」，而是 **整份自定义 Inspector UI 在 Debug 下都不显示**。

---

## 实际用途

- 需要在 **不修改 CustomEditor 代码** 的前提下查看或修改 **未在 Normal 面板暴露的序列化字段** 时，可切换到 **Debug**。
- 若希望 **Normal 模式下也能编辑** 这些字段，应在 `OnInspectorGUI()` 中为对应 `SerializedProperty` 增加 `PropertyField`，或调用 `DrawDefaultInspector()`（会混入默认字段展示，需权衡布局）。

---

## 版本说明

行为以当前 Unity Editor 为准；不同大版本 Inspector 选项名称可能略有差异（如 **Debug** / **Debug Internal**），核心都是：**Debug 视图使用内置属性树，绕过自定义 `OnInspectorGUI`**。

---

*备忘用途：与 `RadialLayout-innerNode-notes.md` 中「通过 Debug Inspector 编辑 `innerNode`」的说明一致。*
