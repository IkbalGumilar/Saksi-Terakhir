using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace SaksiTerakhir.Tests
{
    public sealed class PlayerSeatingAnimationTests
    {
        private const string ControllerPath = "Assets/_Project/Animation/MC_Locomotion.controller";
        private const string ClipRoot = "Assets/_Project/Animations/Relax/RPG-Character@Relax-Sit-";

        [Test]
        public void SeatingUsesTheImportedClipsWithoutReplacingLocomotion()
        {
            AnimatorController controller = LoadController();
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            AnimatorState locomotion = State(machine, "Locomotion");

            Assert.That(machine.defaultState, Is.SameAs(locomotion));
            Assert.That(locomotion.motion, Is.TypeOf<BlendTree>());
            AssertClip(State(machine, "SitDown"), "Sitdown", "Relax-Sit-Sitdown");
            AssertClip(State(machine, "SitIdle"), "Idle", "Relax-Sit-Idle");
            AssertClip(State(machine, "StandUp"), "Standup", "Relax-Sit-Standup");
            Assert.That(((AnimationClip)State(machine, "SitIdle").motion).isLooping, Is.True,
                "The seated pose must keep playing until the character is released.");
        }

        [Test]
        public void SeatedParameterDrivesTheFullEnterAndExitSequence()
        {
            AnimatorController controller = LoadController();
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            AnimatorState locomotion = State(machine, "Locomotion");
            AnimatorState sitDown = State(machine, "SitDown");
            AnimatorState sitIdle = State(machine, "SitIdle");
            AnimatorState standUp = State(machine, "StandUp");

            Assert.That(controller.parameters.Any(parameter => parameter.name == "IsSeated"
                && parameter.type == AnimatorControllerParameterType.Bool), Is.True);
            AssertConditionalTransition(locomotion, sitDown, AnimatorConditionMode.If,
                expectExitTime: false);
            AssertUnconditionalExitTransition(sitDown, sitIdle);
            AssertConditionalTransition(sitIdle, standUp, AnimatorConditionMode.IfNot,
                expectExitTime: false);
            AssertUnconditionalExitTransition(standUp, locomotion);
        }

        private static AnimatorController LoadController()
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(
                ControllerPath);
            Assert.That(controller, Is.Not.Null, ControllerPath);
            return controller;
        }

        private static AnimatorState State(AnimatorStateMachine machine, string name)
        {
            AnimatorState state = machine.states.Select(child => child.state)
                .SingleOrDefault(candidate => candidate.name == name);
            Assert.That(state, Is.Not.Null, name);
            return state;
        }

        private static void AssertClip(AnimatorState state, string fileSuffix, string clipName)
        {
            AnimationClip clip = state.motion as AnimationClip;
            Assert.That(clip, Is.Not.Null, state.name);
            Assert.That(AssetDatabase.GetAssetPath(clip), Is.EqualTo(ClipRoot + fileSuffix + ".FBX"));
            Assert.That(clip.name, Is.EqualTo(clipName));
        }

        private static void AssertConditionalTransition(AnimatorState source, AnimatorState target,
            AnimatorConditionMode mode, bool expectExitTime)
        {
            AnimatorStateTransition transition = source.transitions.SingleOrDefault(candidate =>
                candidate.destinationState == target);
            Assert.That(transition, Is.Not.Null, $"{source.name} -> {target.name}");
            Assert.That(transition.hasExitTime, Is.EqualTo(expectExitTime));
            Assert.That(transition.conditions.Length, Is.EqualTo(1));
            Assert.That(transition.conditions[0].parameter, Is.EqualTo("IsSeated"));
            Assert.That(transition.conditions[0].mode, Is.EqualTo(mode));
        }

        private static void AssertUnconditionalExitTransition(AnimatorState source,
            AnimatorState target)
        {
            AnimatorStateTransition transition = source.transitions.SingleOrDefault(candidate =>
                candidate.destinationState == target);
            Assert.That(transition, Is.Not.Null, $"{source.name} -> {target.name}");
            Assert.That(transition.hasExitTime, Is.True);
            Assert.That(transition.conditions, Is.Empty);
        }
    }
}
