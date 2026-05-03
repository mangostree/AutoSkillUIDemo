using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerAttributes : SingletonBehaviour<PlayerAttributes>
{
    public AttriLinesList AttriLinesUI
    {
        set {
            attriLinesUI = value;
            if (value)
            {
                RefreshattriLinesUI();
            }
        }
    }
    AttriLinesList attriLinesUI = null;

    class AttributeValue
    {
        public AttributeValue(float baseValue)
        {
            this.baseValue = baseValue;
            skilledValue = 0;
        }

        public float value
        {
            get { return baseValue + skilledValue; }
        }

        public float baseValue = 0;
        public float skilledValue = 0;
    }

    Dictionary<string, AttributeValue> attributeDicts = new Dictionary<string, AttributeValue>();

    private void Start()
    {
        LoadBaseAttributes();

        RefreshattriLinesUI();
    }

    void LoadBaseAttributes()
    {
        var asset = Resources.Load<TextAsset>("Sheets/初始属性表");
        if (asset == null)
        {
            Debug.LogError("[PlayerAttributes] 无法加载 Resources/Sheets/初始属性表.bytes");
            return;
        }

        if (!SheetTableBin.TryLoad(asset, out SheetTableBin table))
        {
            Debug.LogError("[PlayerAttributes] 初始属性表.bytes 解析失败");
            return;
        }

        for (int i = 0; i < table.RowCount; i++)
        {
            string attrName = table.GetCell(i, "属性名");
            if (string.IsNullOrEmpty(attrName))
            {
                continue;
            }

            if (!table.TryGetCellAsFloat(i, "生命值", out float baseValue))
            {
                baseValue = 0f;
            }

            attributeDicts[attrName] = new AttributeValue(baseValue);
        }
    }

    public void FetchAttributeChange(string attriName, float value)
    {
        if (attributeDicts.TryGetValue(attriName, out AttributeValue attrValue))
        {
            attrValue.skilledValue += value;
            if (attriLinesUI)
            {
                attriLinesUI.SetAttriValue(attriName, attrValue.value);
            }
        }
    }

    void RefreshattriLinesUI()
    {
        if (attriLinesUI)
        {
            foreach (var kv in attributeDicts)
            {
                attriLinesUI.SetAttriValue(kv.Key, kv.Value.value);
            }
        } 
    }
}
