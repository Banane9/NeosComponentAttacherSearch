using FrooxEngine;
using FrooxEngine.UIX;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ComponentAttacherSearch
{
    internal class AttacherDetails
    {
        private static readonly Type componentAttacherType = typeof(ComponentAttacher);
        private static readonly MethodInfo onAddComponentPressedMethod = componentAttacherType.GetMethod("OnAddComponentPressed", AccessTools.allDeclared);
        private static readonly MethodInfo onCancelPressedMethod = componentAttacherType.GetMethod("OnCancelPressed", AccessTools.allDeclared);
        private static readonly MethodInfo onOpenCategoryPressedMethod = componentAttacherType.GetMethod("OnOpenCategoryPressed", AccessTools.allDeclared);
        private static readonly MethodInfo openGenericTypesPressedMethod = componentAttacherType.GetMethod("OpenGenericTypesPressed", AccessTools.allDeclared);

        public TextEditor Editor => SearchBar.Editor.Target;

        public bool HasSearchBar => SearchBar != null;

        public string LastPath { get; set; }

        public CancellationTokenSource LastResultUpdate { get; set; } = new CancellationTokenSource();

        public string LastSearch { get; set; }

        public ButtonEventHandler<string> OnAddComponentPressed { get; }

        public ButtonEventHandler OnCancelPressed { get; }

        public ButtonEventHandler<string> OnOpenCategoryPressed { get; }

        public ButtonEventHandler<string> OpenGenericTypesPressed { get; }

        public TextField SearchBar { get; set; }

        public Text Text => (Text)Editor.Text.Target;

        public AttacherDetails(ComponentAttacher attacher)
        {
            OnAddComponentPressed = AccessTools.MethodDelegate<ButtonEventHandler<string>>(onAddComponentPressedMethod, attacher);
            OnOpenCategoryPressed = AccessTools.MethodDelegate<ButtonEventHandler<string>>(onOpenCategoryPressedMethod, attacher);
            OpenGenericTypesPressed = AccessTools.MethodDelegate<ButtonEventHandler<string>>(openGenericTypesPressedMethod, attacher);
            OnCancelPressed = AccessTools.MethodDelegate<ButtonEventHandler>(onCancelPressedMethod, attacher);
        }

        public static AttacherDetails New(ComponentAttacher attacher) => new(attacher);
    }
}