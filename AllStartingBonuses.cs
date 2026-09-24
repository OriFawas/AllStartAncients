using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Events;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;

namespace AllStartingBonuses;

[ModInitializer(nameof(Initialize))]
public static class Main
{
    private const string HarmonyId = "com.orifawas.allstartingbonuses";

    public static void Initialize()
    {
        ModConfig.Load();
        Harmony harmony = new(HarmonyId);
        harmony.CreateClassProcessor(typeof(AncientOptionsPatch)).Patch();
        harmony.CreateClassProcessor(typeof(AncientScrollPatch)).Patch();
        harmony.CreateClassProcessor(typeof(DialogueAnimationPatch)).Patch();
        harmony.CreateClassProcessor(typeof(ProceedButtonPositionPatch)).Patch();
        GD.Print($"[AllStartingBonuses] Loaded. UnlockAll: {ModConfig.Current.UnlockAll}, Neow: {ModConfig.ShouldUnlock("Neow")}");
    }

    [HarmonyPatch]
    private static class AncientOptionsPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            Assembly gameAssembly = typeof(AncientEventModel).Assembly;

            foreach (Type type in gameAssembly.GetTypes())
            {
                if (!type.IsAbstract && typeof(AncientEventModel).IsAssignableFrom(type))
                {
                    MethodInfo? method = AccessTools.Method(type, "GenerateInitialOptions");
                    if (method != null)
                    {
                        yield return method;
                    }
                }
            }
        }

        private static bool Prefix(
            AncientEventModel __instance,
            ref IReadOnlyList<EventOption> __result)
        {
            if (__instance.Owner == null)
            {
                return true;
            }

            if (!ModConfig.ShouldUnlockFor(__instance))
            {
                return true;
            }

            List<EventOption> options = __instance.AllPossibleOptions
                .Where(option => option != null)
                .ToList();

            if (options.Count == 0)
            {
                return true;
            }

            __result = options;
            return false;
        }
    }

    [HarmonyPatch(typeof(NAncientEventLayout), "OnSetupComplete")]
    private static class AncientScrollPatch
    {
        public const string ScrollNodeName = "ModOptionsScroll";
        public const float VisibleScrollHeight = 460f;

        private static void Prefix(NAncientEventLayout __instance)
        {
            VBoxContainer? optionsContainer = __instance.GetNodeOrNull<VBoxContainer>("%OptionsContainer");
            if (optionsContainer == null || optionsContainer.GetParent() is ScrollContainer)
            {
                return;
            }

            // Only wrap in ScrollContainer if there are more options than standard fit (3)
            if (optionsContainer.GetChildCount() <= 3)
            {
                return;
            }

            Node? content = optionsContainer.GetParent();
            if (content == null)
            {
                return;
            }

            int index = optionsContainer.GetIndex();
            content.RemoveChild(optionsContainer);

            ScrollContainer scroll = new()
            {
                Name = ScrollNodeName,
                HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
                VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
                CustomMinimumSize = new Vector2(0, VisibleScrollHeight),
                FollowFocus = true,
                SizeFlagsHorizontal = Control.SizeFlags.Fill | Control.SizeFlags.Expand
            };

            optionsContainer.SizeFlagsHorizontal = Control.SizeFlags.Fill | Control.SizeFlags.Expand;

            content.AddChild(scroll);
            content.MoveChild(scroll, index);
            scroll.AddChild(optionsContainer);

            // Allow mouse wheel scrolling to propagate through buttons
            foreach (NEventOptionButton btn in __instance.OptionButtons)
            {
                btn.MouseFilter = Control.MouseFilterEnum.Pass;
            }

            // Forward wheel events over the content container directly to the ScrollContainer
            Control? contentContainer = __instance.GetNodeOrNull<Control>("%ContentContainer");
            if (contentContainer != null)
            {
                contentContainer.GuiInput += (@event) =>
                {
                    if (!GodotObject.IsInstanceValid(scroll))
                    {
                        return;
                    }
                    if (@event is InputEventMouseButton { Pressed: true } mb)
                    {
                        if (mb.ButtonIndex == MouseButton.WheelUp)
                        {
                            scroll.ScrollVertical = Math.Max(0, scroll.ScrollVertical - 100);
                        }
                        else if (mb.ButtonIndex == MouseButton.WheelDown)
                        {
                            scroll.ScrollVertical += 100;
                        }
                    }
                };
            }
        }
    }

    [HarmonyPatch(typeof(NAncientEventLayout), "SetDialogueLineAndAnimate")]
    private static class DialogueAnimationPatch
    {
        private static void Postfix(NAncientEventLayout __instance)
        {
            PropertyInfo? lastLineProp = AccessTools.Property(typeof(NAncientEventLayout), "IsDialogueOnLastLine");
            bool isLastLine = (bool)(lastLineProp?.GetValue(__instance) ?? false);
            if (!isLastLine)
            {
                return;
            }

            ScrollContainer? scroll = __instance.GetNodeOrNull<ScrollContainer>($"%{AncientScrollPatch.ScrollNodeName}")
                ?? __instance.GetNodeOrNull<ScrollContainer>($"%Content/{AncientScrollPatch.ScrollNodeName}");

            if (scroll == null)
            {
                return;
            }

            FieldInfo? tweenField = AccessTools.Field(typeof(NAncientEventLayout), "_contentTween");
            FieldInfo? contentField = AccessTools.Field(typeof(NAncientEventLayout), "_content");
            FieldInfo? contentContainerField = AccessTools.Field(typeof(NAncientEventLayout), "_contentContainer");
            FieldInfo? dialogueContainerField = AccessTools.Field(typeof(NAncientEventLayout), "_dialogueContainer");
            FieldInfo? currentLineField = AccessTools.Field(typeof(NAncientEventLayout), "_currentDialogueLine");

            if (tweenField == null || contentField == null || contentContainerField == null || dialogueContainerField == null)
            {
                return;
            }

            // Kill the vanilla tween that calculated position using the huge unbound optionsContainer.Size.Y
            Tween? oldTween = (Tween?)tweenField.GetValue(__instance);
            oldTween?.Kill();

            Control contentContainer = (Control)contentContainerField.GetValue(__instance)!;
            VBoxContainer content = (VBoxContainer)contentField.GetValue(__instance)!;
            VBoxContainer dialogueContainer = (VBoxContainer)dialogueContainerField.GetValue(__instance)!;
            int currentLine = (int)(currentLineField?.GetValue(__instance) ?? 0);

            Control? currentDialogue = dialogueContainer.GetChildOrNull<Control>(currentLine);
            float dialogueBottom = currentDialogue != null ? (currentDialogue.Position.Y + currentDialogue.Size.Y) : 0f;

            // Recalculate target Y position using the ScrollContainer's constrained height
            float num = dialogueBottom + scroll.CustomMinimumSize.Y + 10f;
            float targetY = contentContainer.Size.Y - num;

            Tween newTween = __instance.CreateTween();
            tweenField.SetValue(__instance, newTween);

            newTween.TweenProperty(content, "position", new Vector2(content.Position.X, targetY), 1.0)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Expo);

            newTween.Parallel().TweenCallback(Callable.From(() =>
            {
                foreach (NEventOptionButton optionButton in __instance.OptionButtons)
                {
                    optionButton.FocusMode = Control.FocusModeEnum.All;
                    optionButton.MouseFilter = Control.MouseFilterEnum.Pass;
                }
                __instance.DefaultFocusedControl?.TryGrabFocus();
            })).SetDelay(0.8);
        }
    }

    [HarmonyPatch(typeof(NEventLayout), "AddOptions")]
    private static class ProceedButtonPositionPatch
    {
        private static void Postfix(NEventLayout __instance, IEnumerable<EventOption> options)
        {
            if (__instance is not NAncientEventLayout ancientLayout)
            {
                return;
            }

            List<EventOption> optList = options?.ToList() ?? new List<EventOption>();
            bool isProceed = optList.Count == 1 && optList[0].IsProceed;

            if (!isProceed)
            {
                return;
            }

            // Only run if this ancient was wrapped in a ScrollContainer
            ScrollContainer? scroll = ancientLayout.GetNodeOrNull<ScrollContainer>($"%{AncientScrollPatch.ScrollNodeName}")
                ?? ancientLayout.GetNodeOrNull<ScrollContainer>($"%Content/{AncientScrollPatch.ScrollNodeName}");

            if (scroll == null)
            {
                return;
            }

            // Defer to next idle frame so ClearOptions QueueFree and child additions finish cleanly
            Callable.From(() =>
            {
                if (!GodotObject.IsInstanceValid(ancientLayout))
                {
                    return;
                }

                VBoxContainer? optionsContainer = ancientLayout.GetNodeOrNull<VBoxContainer>("%OptionsContainer");
                Control? contentContainer = ancientLayout.GetNodeOrNull<Control>("%ContentContainer");
                VBoxContainer? content = ancientLayout.GetNodeOrNull<VBoxContainer>("%Content");

                if (content == null || optionsContainer == null || contentContainer == null)
                {
                    return;
                }

                // If optionsContainer was wrapped in ScrollContainer, unwrap it back into %Content
                if (scroll != null && optionsContainer.GetParent() == scroll)
                {
                    int index = scroll.GetIndex();
                    scroll.RemoveChild(optionsContainer);
                    content.RemoveChild(scroll);
                    content.AddChild(optionsContainer);
                    content.MoveChild(optionsContainer, index);
                    scroll.QueueFree();
                }

                // Restore content layout flags
                optionsContainer.SizeFlagsHorizontal = Control.SizeFlags.Fill | Control.SizeFlags.Expand;
                content.Alignment = BoxContainer.AlignmentMode.Begin;

                // Restore contentContainer to full original height
                FieldInfo? origHeightField = AccessTools.Field(typeof(NAncientEventLayout), "_originalContentContainerHeight");
                float origHeight = (float)(origHeightField?.GetValue(ancientLayout) ?? contentContainer.Size.Y);
                if (origHeight > 100f)
                {
                    contentContainer.Size = new Vector2(contentContainer.Size.X, origHeight);
                }

                // Stop any previous tween on content
                FieldInfo? tweenField = AccessTools.Field(typeof(NAncientEventLayout), "_contentTween");
                Tween? oldTween = (Tween?)tweenField?.GetValue(ancientLayout);
                oldTween?.Kill();

                // Find the proceed button and ensure it is fully enabled, visible, and interactive
                NEventOptionButton? proceedBtn = ancientLayout.OptionButtons.FirstOrDefault();
                if (proceedBtn != null)
                {
                    proceedBtn.Modulate = Colors.White;
                    proceedBtn.MouseFilter = Control.MouseFilterEnum.Stop;
                    proceedBtn.FocusMode = Control.FocusModeEnum.All;
                }

                // Measure button height (standard STS2 button is ~130px)
                float btnHeight = (proceedBtn != null && proceedBtn.Size.Y > 50f) ? proceedBtn.Size.Y : 130f;

                // Target Y docks the button 10px above the bottom of %ContentContainer
                float containerHeight = contentContainer.Size.Y > 100f ? contentContainer.Size.Y : 720f;
                float targetY = containerHeight - btnHeight - 10f;

                // Smoothly slide %Content down to targetY at the bottom dock
                Tween tween = ancientLayout.CreateTween();
                tweenField?.SetValue(ancientLayout, tween);

                tween.TweenProperty(content, "position", new Vector2(content.Position.X, targetY), 0.5)
                    .SetEase(Tween.EaseType.Out)
                    .SetTrans(Tween.TransitionType.Expo);

                tween.Parallel().TweenCallback(Callable.From(() =>
                {
                    if (GodotObject.IsInstanceValid(proceedBtn))
                    {
                        proceedBtn.EnableButton();
                        proceedBtn.TryGrabFocus();
                    }
                })).SetDelay(0.3);
            }).CallDeferred();
        }
    }
}
