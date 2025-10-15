using UnityEngine;
using UnityEngine.UI;

//数据结构体
[System.Serializable]
public struct TestGridData
{
    public Sprite iconSprite;
    public string nameString;
}

//对应的UI视图
public class TestGrid : MonoBehaviour
{
    public Image iconImage;
    public Text descText;

    public void UpdateDisplay(Sprite _sprite, string _text)
    {
        iconImage.sprite = _sprite;
        descText.text = _text;
    }
}