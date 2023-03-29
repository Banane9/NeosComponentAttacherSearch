using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using BaseX;
using FrooxEngine;
using FrooxEngine.UIX;
using HarmonyLib;

namespace ComponentAttacherSearch
{
    public partial class ComponentAttacherSearch
    {
        [HarmonyPatch(typeof(ComponentAttacher), "BuildUI")]
        private static class ComponentAttacherPatch
        {
            private const string searchPath = "/Search/";
            private static readonly ConditionalWeakTable<ComponentAttacher, AttacherDetails> attacherDetails = new();
            private static readonly color cancelColor = new(1f, 0.8f, 0.8f, 1f);
            private static readonly color categoryColor = new(1f, 1f, 0.8f, 1f);
            private static readonly color genericTypeColor = new(0.8f, 1f, 0.8f, 1f);
            private static readonly color typeColor = new(0.8f, 0.8f, 1f, 1f);

            private static void AddSearchbar(ComponentAttacher attacher, AttacherDetails details)
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
                details.SearchBar = builder.TextField(null, parseRTF: false);
                details.Text.NullContent.Value = "<i>Search</i>";
                details.Editor.FinishHandling.Value = TextEditor.FinishAction.NullOnWhitespace;
                details.Text.Content.OnValueChange += MakeBuildUICall(attacher, details);

                builder = new UIBuilder(footer);
                MakeLocalButton(builder, "∅", (sender) => details.Text.Content.Value = null);
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
                        ClearSearchbar(__instance);
                        details.SearchBar = null;
                    }

                    return true;
                }

                var componentLibrary = PickComponentLibrary(ref path, out var search);
                details.LastPath = path;

                if (details.SearchBar == null)
                {
                    AddSearchbar(__instance, details);
                    details.Text.Content.Value = search;
                }

                if (search == null && !details.SearchBar.Editor.Target.IsEditing)
                    details.Text.Content.Value = null;

                if (string.IsNullOrWhiteSpace(search) || (search.Length < 3 && path.Length < 2))
                    return true;

                var builder = new UIBuilder(__instance._uiRoot);
                builder.Root.DestroyChildren();
                builder.Style.MinHeight = 32f;

                foreach (var subCategory in SearchCategories(componentLibrary, search))
                {
                    var categoryPath = subCategory.GetPath();
                    builder.Button(categoryPath.Substring(1).Replace("/", " > ") + " >", categoryColor, details.OnOpenCategoryPressed, subCategory.GetPath(), 0.35f).Label.ParseRichText.Value = false;
                }

                foreach (var type in SearchTypes(componentLibrary, search))
                {
                    var name = type.GetNiceName();

                    if (type.IsGenericTypeDefinition)
                        builder.Button(name, genericTypeColor, details.OpenGenericTypesPressed, Path.Combine(path + searchPath + search, type.FullName), 0.35f).Label.ParseRichText.Value = false;
                    else
                        builder.Button(name, typeColor, details.OnAddComponentPressed, type.FullName, 0.35f).Label.ParseRichText.Value = false;
                }

                builder.Button("Cancel", cancelColor, details.OnCancelPressed, 0.35f);

                return false;
            }

            private static void ClearSearchbar(ComponentAttacher attacher)
            {
                var contentRoot = attacher._uiRoot.Target.Parent;

                contentRoot.Parent = contentRoot.Parent.Parent;
                contentRoot.Parent.DestroyChildren(filter: slot => slot != contentRoot);
            }

            private static SyncFieldEvent<string> MakeBuildUICall(ComponentAttacher attacher, AttacherDetails details)
            {
                return field =>
                {
                    details.LastResultUpdate.Cancel();
                    details.LastResultUpdate = new CancellationTokenSource();
                    var token = details.LastResultUpdate.Token;

                    Task.Run(async () =>
                    {
                        var delay = Config.GetValue(SearchRefreshDelay);
                        if (delay > 0)
                            await Task.Delay(delay);

                        // Only refresh UI with search results if there was no further update immediately following it
                        if (token.IsCancellationRequested)
                            return;

                        attacher.RunSynchronously(() => attacher.BuildUI(attacherDetails.GetOrCreateValue(attacher).LastPath + searchPath + field.Value, false));
                    });
                };
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

            private static CategoryNode<Type> PickComponentLibrary(ref string path, out string search)
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

            private static IEnumerable<CategoryNode<Type>> SearchCategories(CategoryNode<Type> root, string search = null)
            {
                var returnAll = search == null;
                var queue = new Queue<CategoryNode<Type>>();

                foreach (var subCategory in root.Subcategories)
                    queue.Enqueue(subCategory);

                while (queue.Count > 0)
                {
                    var category = queue.Dequeue();

                    if (ExcludedCategories.Contains(category.GetPath()))
                        continue;

                    if (returnAll || SearchContains(category.Name, search))
                        yield return category;

                    foreach (var subCategory in category.Subcategories)
                        queue.Enqueue(subCategory);
                }
            }

            private static bool SearchContains(string haystack, string needle)
                => CultureInfo.InvariantCulture.CompareInfo.IndexOf(haystack, needle, CompareOptions.IgnoreCase) >= 0;

            private static IEnumerable<Type> SearchTypes(CategoryNode<Type> root, string search)
                => root.Elements.Concat(SearchCategories(root).SelectMany(category => category.Elements)).Where(type => SearchContains(type.Name, search));
        }
    }
}