using TMPro;
using UnityEngine;

/// <summary>
/// TMP_Dropdown, который строит выпадающую панель прямо под своим шаблоном
/// (Template -> его родитель), а не в корне сцены. Это нужно, потому что
/// стандартный TMP_Dropdown создаёт клон шаблона как корневой объект, и если
/// у шаблона есть Canvas, клон на один кадр становится корневым Canvas
/// (Screen Space Overlay) и Unity растягивает его на весь экран — из-за чего
/// панель открывается не там и неверного размера.
/// </summary>
public class RunModifierDropdown : TMP_Dropdown
{
    protected override GameObject CreateDropdownList(GameObject template)
    {
        Transform parent = template.transform.parent;
        GameObject list = Instantiate(template, parent, false);
        return list;
    }
}
