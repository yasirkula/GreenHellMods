using System;
using System.Reflection;
using RuntimeUnityEditor.Core.Utils;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RuntimeUnityEditor.Core.UI
{
    public static class InterfaceMaker
    {
        public static void EatInputInRect(Rect eatRect)
        {
            if (eatRect.Contains(new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y)))
                Input.ResetInputAxes();
        }
    }
}
