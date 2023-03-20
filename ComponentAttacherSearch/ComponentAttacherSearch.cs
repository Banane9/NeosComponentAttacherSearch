using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using BaseX;
using CodeX;
using FrooxEngine;
using FrooxEngine.LogiX;
using FrooxEngine.LogiX.Data;
using FrooxEngine.LogiX.ProgramFlow;
using FrooxEngine.UIX;
using HarmonyLib;
using NeosModLoader;

namespace ComponentAttacherSearch
{
    public class ComponentAttacherSearch : NeosMod
    {
        public override string Author => "Banane9";
        public override string Link => "https://github.com/Banane9/NeosComponentAttacherSearch";
        public override string Name => "ComponentAttacherSearch";
        public override string Version => "1.0.0";

        public override void OnEngineInit()
        {
            Harmony harmony = new Harmony($"{Author}.{Name}");
            harmony.PatchAll();
        }

        [HarmonyPatch(typeof(ComponentAttacher), "BuildUI")]
        private static class ComponentAttacherPatch
        {
            private const string searchPath = "/Search/";
            private static readonly ConditionalWeakTable<ComponentAttacher, AttacherDetails> attacherDetails = new();
            private static readonly MethodInfo buildUIMethod = typeof(ComponentAttacher).GetMethod("BuildUI", AccessTools.allDeclared);
            private static readonly color cancelColor = new(1f, 0.8f, 0.8f, 1f);
            private static readonly color categoryColor = new(1f, 1f, 0.8f, 1f);
            private static readonly color genericTypeColor = new(0.8f, 1f, 0.8f, 1f);
            private static readonly color typeColor = new(0.8f, 0.8f, 1f, 1f);

            private static TextField addSearchbar(ComponentAttacher attacher)
            {
                var uiRoot = (SyncRef<Slot>)attacher.TryGetField("_uiRoot");

                var builder = new UIBuilder(uiRoot.Target.Parent.Parent);

                builder.HorizontalHeader(48, out var header, out var content);
                uiRoot.Target.Parent.Parent = content.Slot;

                header.OffsetMin.Value += new float2(8, 8);
                header.OffsetMax.Value += new float2(-8, -8);

                builder = new UIBuilder(header);
                builder.VerticalFooter(36, out var footer, out content);
                footer.OffsetMin.Value += new float2(4, 0);

                builder = new UIBuilder(content);
                var textField = builder.TextField(null, parseRTF: false);
                ((Text)textField.Editor.Target.Text.Target).NullContent.Value = "<i>Search</i>";
                textField.Editor.Target.FinishHandling.Value = TextEditor.FinishAction.NullOnWhitespace;
                getTextContent(textField).OnValueChange += makeBuildUICall(attacher);

                builder = new UIBuilder(footer);
                MakeLocalButton(builder, "∅", (sender) => textField.Editor.Target.Text.Target.Text = null);

                return textField;
            }

            [HarmonyPrefix]
            private static bool BuildUIPrefix(ComponentAttacher __instance, ref string path, bool genericType)
            {
                if (!attacherDetails.TryGetValue(__instance, out var details))
                {
                    details = new AttacherDetails(__instance);
                    attacherDetails.Add(__instance, details);
                }

                if (genericType)
                {
                    if (details.HasSearchBar)
                    {
                        clearSearchbar(__instance);
                        details.SearchBar = null;
                    }

                    return true;
                }

                var componentLibrary = pickComponentLibrary(ref path, out var search);
                details.LastPath = path;

                if (details.SearchBar == null)
                {
                    details.SearchBar = addSearchbar(__instance);
                    getTextContent(details.SearchBar).Value = search;
                }

                if (search == null && !details.SearchBar.Editor.Target.IsEditing)
                    getTextContent(details.SearchBar).Value = null;

                if (string.IsNullOrWhiteSpace(search) || (search.Length < 3 && path.Length < 2))
                    return true;

                var builder = new UIBuilder((SyncRef<Slot>)__instance.TryGetField("_uiRoot"));
                builder.Root.DestroyChildren();
                builder.Style.MinHeight = 32f;

                foreach (var subCategory in searchCategories(componentLibrary, search))
                {
                    var categoryPath = subCategory.GetPath();
                    builder.Button(categoryPath.Substring(1).Replace("/", " > ") + " >", categoryColor, details.OnOpenCategoryPressed, subCategory.GetPath(), 0.35f).Label.ParseRichText.Value = false;
                }

                foreach (var type in searchTypes(componentLibrary, search))
                {
                    var name = type.GetNiceName("<", ">");

                    if (type.IsGenericTypeDefinition)
                        builder.Button(name, genericTypeColor, details.OpenGenericTypesPressed, Path.Combine(path + searchPath + search, type.FullName), 0.35f).Label.ParseRichText.Value = false;
                    else
                        builder.Button(name, typeColor, details.OnAddComponentPressed, type.FullName, 0.35f).Label.ParseRichText.Value = false;
                }

                builder.Button("Cancel", cancelColor, details.OnCancelPressed, 0.35f);

                return false;
            }

            private static void clearSearchbar(ComponentAttacher attacher)
            {
                var contentRoot = attacher._uiRoot.Target.Parent;

                contentRoot.Parent = contentRoot.Parent.Parent;
                contentRoot.Parent.DestroyChildren(filter: slot => slot != contentRoot);
            }

            private static SyncField<string> getTextContent(TextField textField)
                => ((Text)textField.Editor.Target.Text.Target).Content;

            private static SyncFieldEvent<string> makeBuildUICall(ComponentAttacher attacher)
            {
                return field => attacher.BuildUI(attacherDetails.GetOrCreateValue(attacher).LastPath + searchPath + field.Value, false);
            }

            private static Button MakeLocalButton(UIBuilder builder, LocaleString text, Action<IButton> action)
            {
                var button = builder.Button(text);

                var valueField = button.Slot.AttachComponent<ValueField<bool>>().Value;
                var toggle = button.Slot.AttachComponent<ButtonToggle>();
                toggle.TargetValue.Target = valueField;

                valueField.OnValueChange += field => action(button);

                return button;
            }

            private static CategoryNode<Type> pickComponentLibrary(ref string path, out string search)
            {
                search = null;

                if (string.IsNullOrEmpty(path) || path == "/")
                {
                    path = "";
                    return WorkerInitializer.ComponentLibrary;
                }

                path = path.Replace('\\', '/');

                var searchIndex = path.IndexOf(searchPath);
                if (searchIndex >= 0)
                {
                    if (searchIndex + searchPath.Length < path.Length)
                        search = path.Substring(searchIndex + searchPath.Length).Replace(" ", "");

                    path = path.Remove(searchIndex);
                }

                var categoryNode = WorkerInitializer.ComponentLibrary.GetSubcategory(path);
                if (categoryNode == null)
                {
                    path = "";
                    return WorkerInitializer.ComponentLibrary;
                }

                return categoryNode;
            }

            private static IEnumerable<CategoryNode<Type>> searchCategories(CategoryNode<Type> root, string search = null)
            {
                var returnAll = search == null;
                var includeFavorites = root.Name == "Favorites";

                var queue = new Queue<CategoryNode<Type>>();

                foreach (var subCategory in root.Subcategories)
                    queue.Enqueue(subCategory);

                while (queue.Count > 0)
                {
                    var category = queue.Dequeue();

                    if (category.Name == "Favorites" && !includeFavorites)
                        continue;

                    if (returnAll || searchContains(category.Name, search))
                        yield return category;

                    foreach (var subCategory in category.Subcategories)
                        queue.Enqueue(subCategory);
                }
            }

            private static bool searchContains(string haystack, string needle)
                => CultureInfo.InvariantCulture.CompareInfo.IndexOf(haystack, needle, CompareOptions.IgnoreCase) >= 0;

            private static IEnumerable<Type> searchTypes(CategoryNode<Type> root, string search)
                => root.Elements.Concat(searchCategories(root).SelectMany(category => category.Elements)).Where(type => searchContains(type.Name, search));
        }
    }
}