using System;
using System.Collections;
using System.IO;
using System.Reflection;
using RuntimeUnityEditor.Core.ObjectTree;
using RuntimeUnityEditor.Core.UI;
using RuntimeUnityEditor.Core.Utils;
using UnityEngine;

namespace RuntimeUnityEditor.Core
{
    public class RuntimeUnityEditorCore
    {
        public const string Version = "2.3";
        public const string GUID = "RuntimeUnityEditor";

        public Inspector.Inspector Inspector { get; }
        public ObjectTreeViewer TreeViewer { get; }

        public bool EnableMouseInspect
        {
            get => MouseInspect.Enable;
            set
            {
                if (MouseInspect.Enable != value)
                    MouseInspect.Enable = value;
            }
        }

        public bool ShowInspector
        {
            get => Inspector != null && Inspector.Show;
            set
            {
                if (Inspector != null && Inspector.Show != value)
                    Inspector.Show = value;
            }
        }

        public static RuntimeUnityEditorCore Instance { get; private set; }
        internal static MonoBehaviour PluginObject { get; private set; }
        internal static ILoggerWrapper Logger { get; private set; }

        private readonly GameObjectSearcher _gameObjectSearcher = new GameObjectSearcher();

        internal RuntimeUnityEditorCore(MonoBehaviour pluginObject, ILoggerWrapper logger)
        {
            if (Instance != null)
                throw new InvalidOperationException("Can only create one instance of the Core object");

            PluginObject = pluginObject;
            Logger = logger;
            Instance = this;

            // Reflection for compatibility with Unity 4.x
            var tCursor = typeof(Cursor);
            
            Inspector = new Inspector.Inspector(targetTransform => TreeViewer.SelectAndShowObject(targetTransform));

            TreeViewer = new ObjectTreeViewer(pluginObject, _gameObjectSearcher);
            TreeViewer.InspectorOpenCallback = items =>
            {
                for (var i = 0; i < items.Length; i++)
                {
                    var stackEntry = items[i];
                    Inspector.Push(stackEntry, i == 0);
                }
            };
        }

        internal void OnGUI()
        {
            if (Show)
            {
                Inspector.DisplayInspector();
                TreeViewer.DisplayViewer();
                
                MouseInspect.OnGUI();
            }
        }

        public bool Show
        {
            get => TreeViewer.Enabled;
            set
            {
                TreeViewer.Enabled = value;

                if (value)
                {
                    SetWindowSizes();

                    RefreshGameObjectSearcher(true);
                }
            }
        }

        private float prevUpdateTime;
        public float HierarchyUpdateInterval;

        internal void Update()
        {
            if (Show)
            {
                if( Time.realtimeSinceStartup - prevUpdateTime >= HierarchyUpdateInterval )
                {
                    prevUpdateTime = Time.realtimeSinceStartup;
                    RefreshGameObjectSearcher( false );
                }

                TreeViewer.Update();

                MouseInspect.Update();
            }
        }

        private void RefreshGameObjectSearcher(bool full)
        {
            _gameObjectSearcher.Refresh(full, null);
        }

        private void SetWindowSizes()
        {
            const int screenOffset = 10;

            var screenRect = new Rect(
                screenOffset,
                screenOffset,
                Screen.width - screenOffset * 2,
                Screen.height - screenOffset * 2);

            var centerWidth = (int)Mathf.Min(850, screenRect.width);
            var centerX = (int)(screenRect.xMin + screenRect.width / 2 - Mathf.RoundToInt((float)centerWidth / 2));

            var inspectorHeight = (int)(screenRect.height / 4) * 3;
            Inspector.UpdateWindowSize(new Rect(
                centerX,
                screenRect.yMin,
                centerWidth,
                inspectorHeight));

            var rightWidth = 350;
            var treeViewHeight = screenRect.height;
            TreeViewer.UpdateWindowSize(new Rect(
                screenRect.xMax - rightWidth,
                screenRect.yMin,
                rightWidth,
                treeViewHeight));
        }
    }
}
