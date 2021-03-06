﻿using System;
using System.Collections.Generic;
using System.Linq;
using UnityExplorer.Helpers;
using UnityExplorer.UI;
using UnityExplorer.UI.Shared;
using UnityExplorer.Unstrip;
//using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityExplorer.Input;

namespace UnityExplorer.Inspectors.GameObjects
{
    public class ComponentList
    {
        internal static ComponentList Instance;

        public ComponentList()
        {
            Instance = this;
        }

        public static PageHandler s_compListPageHandler;
        private static Component[] s_allComps = new Component[0];
        private static readonly List<Component> s_compShortlist = new List<Component>();
        private static GameObject s_compListContent;
        private static readonly List<Text> s_compListTexts = new List<Text>();
        private static int s_lastCompCount;
        public static readonly List<Toggle> s_compToggles = new List<Toggle>();

        internal void RefreshComponentList()
        {
            var go = GameObjectInspector.ActiveInstance.TargetGO;

            s_allComps = go.GetComponents<Component>().ToArray();

            var components = s_allComps;
            s_compListPageHandler.ListCount = components.Length;

            //int startIndex = m_sceneListPageHandler.StartIndex;

            int newCount = 0;

            foreach (var itemIndex in s_compListPageHandler)
            {
                newCount++;

                // normalized index starting from 0
                var i = itemIndex - s_compListPageHandler.StartIndex;

                if (itemIndex >= components.Length)
                {
                    if (i > s_lastCompCount || i >= s_compListTexts.Count)
                        break;

                    GameObject label = s_compListTexts[i].transform.parent.parent.gameObject;
                    if (label.activeSelf)
                        label.SetActive(false);
                }
                else
                {
                    Component comp = components[itemIndex];

                    if (!comp)
                        continue;

                    if (i >= s_compShortlist.Count)
                    {
                        s_compShortlist.Add(comp);
                        AddCompListButton();
                    }
                    else
                    {
                        s_compShortlist[i] = comp;
                    }

                    var text = s_compListTexts[i];

                    text.text = UISyntaxHighlight.ParseFullSyntax(ReflectionHelpers.GetActualType(comp), true);

                    var toggle = s_compToggles[i];
#if CPP
                    if (comp.TryCast<Behaviour>() is Behaviour behaviour)
#else
                    if (comp is Behaviour behaviour)
#endif
                    {
                        if (!toggle.gameObject.activeSelf)
                            toggle.gameObject.SetActive(true);

                        toggle.isOn = behaviour.enabled;
                    }
                    else
                    {
                        if (toggle.gameObject.activeSelf)
                            toggle.gameObject.SetActive(false);
                    }

                    var label = text.transform.parent.parent.gameObject;
                    if (!label.activeSelf)
                    {
                        label.SetActive(true);
                    }
                }
            }

            s_lastCompCount = newCount;
        }

        internal static void OnCompToggleClicked(int index, bool value)
        {
            var comp = s_compShortlist[index];
#if CPP
            comp.TryCast<Behaviour>().enabled = value;
#else
            (comp as Behaviour).enabled = value;
#endif
        }

        internal static void OnCompListObjectClicked(int index)
        {
            if (index >= s_compShortlist.Count || !s_compShortlist[index])
            {
                return;
            }

            InspectorManager.Instance.Inspect(s_compShortlist[index]);
        }

        internal static void OnCompListPageTurn()
        {
            if (Instance == null)
                return;

            Instance.RefreshComponentList();
        }


#region UI CONSTRUCTION

        internal void ConstructCompList(GameObject parent)
        {
            var vertGroupObj = UIFactory.CreateVerticalGroup(parent, new Color(1, 1, 1, 0));
            var vertGroup = vertGroupObj.GetComponent<VerticalLayoutGroup>();
            vertGroup.childForceExpandHeight = true;
            vertGroup.childForceExpandWidth = false;
            vertGroup.childControlWidth = true;
            vertGroup.spacing = 5;
            var vertLayout = vertGroupObj.AddComponent<LayoutElement>();
            vertLayout.minWidth = 120;
            vertLayout.flexibleWidth = 25000;
            vertLayout.minHeight = 200;
            vertLayout.flexibleHeight = 5000;

            var compTitleObj = UIFactory.CreateLabel(vertGroupObj, TextAnchor.MiddleLeft);
            var compTitleText = compTitleObj.GetComponent<Text>();
            compTitleText.text = "Components";
            compTitleText.color = Color.grey;
            compTitleText.fontSize = 14;
            var childTitleLayout = compTitleObj.AddComponent<LayoutElement>();
            childTitleLayout.minHeight = 30;

            var compScrollObj = UIFactory.CreateScrollView(vertGroupObj, out s_compListContent, out SliderScrollbar scroller, new Color(0.07f, 0.07f, 0.07f));
            var contentLayout = compScrollObj.AddComponent<LayoutElement>();
            contentLayout.minHeight = 50;
            contentLayout.flexibleHeight = 5000;

            s_compListPageHandler = new PageHandler(scroller);
            s_compListPageHandler.ConstructUI(vertGroupObj);
            s_compListPageHandler.OnPageChanged += OnCompListPageTurn;
        }

        internal void AddCompListButton()
        {
            int thisIndex = s_compListTexts.Count;

            GameObject groupObj = UIFactory.CreateHorizontalGroup(s_compListContent, new Color(0.07f, 0.07f, 0.07f));
            HorizontalLayoutGroup group = groupObj.GetComponent<HorizontalLayoutGroup>();
            group.childForceExpandWidth = true;
            group.childControlWidth = true;
            group.childForceExpandHeight = false;
            group.childControlHeight = true;
            group.childAlignment = TextAnchor.MiddleLeft;
            LayoutElement groupLayout = groupObj.AddComponent<LayoutElement>();
            groupLayout.minWidth = 25;
            groupLayout.flexibleWidth = 999;
            groupLayout.minHeight = 25;
            groupLayout.flexibleHeight = 0;
            groupObj.AddComponent<Mask>();

            // Behaviour enabled toggle

            var toggleObj = UIFactory.CreateToggle(groupObj, out Toggle toggle, out Text toggleText, new Color(0.3f, 0.3f, 0.3f));
            var toggleLayout = toggleObj.AddComponent<LayoutElement>();
            toggleLayout.minHeight = 25;
            toggleLayout.minWidth = 25;
            toggleText.text = "";
            toggle.isOn = true;
            s_compToggles.Add(toggle);
            toggle.onValueChanged.AddListener((bool val) => { OnCompToggleClicked(thisIndex, val); });

            // Main component button

            GameObject mainButtonObj = UIFactory.CreateButton(groupObj);
            LayoutElement mainBtnLayout = mainButtonObj.AddComponent<LayoutElement>();
            mainBtnLayout.minHeight = 25;
            mainBtnLayout.flexibleHeight = 0;
            mainBtnLayout.minWidth = 25;
            mainBtnLayout.flexibleWidth = 999;
            Button mainBtn = mainButtonObj.GetComponent<Button>();
            ColorBlock mainColors = mainBtn.colors;
            mainColors.normalColor = new Color(0.07f, 0.07f, 0.07f);
            mainColors.highlightedColor = new Color(0.2f, 0.2f, 0.2f, 1);
            mainBtn.colors = mainColors;
            mainBtn.onClick.AddListener(() => { OnCompListObjectClicked(thisIndex); });

            // Component button text

            Text mainText = mainButtonObj.GetComponentInChildren<Text>();
            mainText.alignment = TextAnchor.MiddleLeft;
            mainText.horizontalOverflow = HorizontalWrapMode.Overflow;
            //mainText.color = SyntaxColors.Class_Instance.ToColor();
            mainText.resizeTextForBestFit = true;
            mainText.resizeTextMaxSize = 14;
            mainText.resizeTextMinSize = 8;

            s_compListTexts.Add(mainText);

            // TODO remove component button
        }


#endregion
    }
}
