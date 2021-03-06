using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System;

[CustomEditor(typeof(ActionSkillDef))]
public class ActionSkillDefEditor : SkillDefEditor {
	[MenuItem("SRPGCK/Create action skill", false, 22)]
	public static ActionSkillDef CreateActionSkillDef()
	{
		ActionSkillDef sd = ScriptableObjectUtility.CreateAsset<ActionSkillDef>(
			null,
			"Assets/SRPGCK Data/Skills/Action",
			true
		);
		sd.isEnabledF = Formula.True();
		sd.io = SRPGCKSettings.Settings.defaultActionIO;
		sd.reallyDefined = true;
		return sd;
	}
	protected ActionSkillDef atk;

 	public override void OnEnable() {
		base.OnEnable();
		name = "ActionSkillDef";
		atk = target as ActionSkillDef;
	}
	protected virtual void TargetedSkillGUI() {
		if(!(target is MoveSkillDef)) {
			atk.turnToFaceTarget = EditorGUILayout.Toggle("Face Target", atk.turnToFaceTarget);
		}
		atk.delay = EditorGUIExt.FormulaField("Scheduled Delay", atk.delay, atk.GetInstanceID()+"."+atk.name+".delay", formulaOptions, lastFocusedControl);
		if(Formula.NotNullFormula(atk.delay) &&
		   !(atk.delay.formulaType == FormulaType.Constant &&
		     atk.delay.constantValue == 0)) {
	 		atk.delayedApplicationUsesOriginalPosition = EditorGUILayout.Toggle("Trigger from Original Position", atk.delayedApplicationUsesOriginalPosition);
		}
		if(atk.targetSettings == null) {
			atk.targetSettings = new TargetSettings[]{new TargetSettings()};
		}
		if((atk.multiTargetMode = (MultiTargetMode)EditorGUILayout.EnumPopup("Multi-Target Mode", atk.multiTargetMode)) != MultiTargetMode.Single) {
			if(atk.multiTargetMode == MultiTargetMode.Chain) {
				atk.maxWaypointDistanceF = EditorGUIExt.FormulaField("Max Waypoint Distance", atk.maxWaypointDistanceF, atk.GetInstanceID()+"."+atk.name+".targeting.maxWaypointDistance", formulaOptions, lastFocusedControl);
			}
			atk.waypointsAreIncremental = EditorGUILayout.Toggle("Instantly Apply Waypoints", atk.waypointsAreIncremental);
			atk.canCancelWaypoints = EditorGUILayout.Toggle("Cancellable Waypoints", atk.canCancelWaypoints);
			EditorGUILayout.BeginVertical();
			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.Space();
			int arraySize = EditorGUILayout.IntField(atk.targetSettings.Length, GUILayout.Width(32));
			GUILayout.Label(" "+"Target"+(atk.targetSettings.Length == 1 ? "" : "s"));
			GUILayout.FlexibleSpace();
			EditorGUILayout.EndHorizontal();
			var oldSettings = atk.targetSettings;
			if(arraySize != atk.targetSettings.Length) {
				TargetSettings[] newSettings = atk.targetSettings;
				Array.Resize(ref newSettings, arraySize);
				atk.targetSettings = newSettings;
			}
			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.Space();
			EditorGUILayout.BeginVertical();
			for(int i = 0; i < atk.targetSettings.Length; i++)
			{
				TargetSettings ts = i < oldSettings.Length ? oldSettings[i] : atk.targetSettings[i];
				if (ts == null) {
					atk.targetSettings[i] = new TargetSettings();
					ts = atk.targetSettings[i];
				}
				atk.targetSettings[i] = EditorGUIExt.TargetSettingsGUI("Target "+i, atk.targetSettings[i], atk, formulaOptions, lastFocusedControl, i);
			}
			EditorGUILayout.EndVertical();
			EditorGUILayout.EndHorizontal();
			EditorGUILayout.EndVertical();
		} else {
			atk.targetSettings[0] = EditorGUIExt.TargetSettingsGUI("Target", atk.targetSettings[0], atk, formulaOptions, lastFocusedControl, -1);
		}
	}

	protected virtual void EffectSkillGUI() {
		atk.involvedItem = EditorGUIExt.PickAssetGUI<Item>("Involved Item", atk.involvedItem);
		if(Formula.NotNullFormula(atk.delay) &&
		   !(atk.delay.formulaType == FormulaType.Constant &&
		     atk.delay.constantValue == 0)) {
	 		atk.scheduledEffects = EditorGUIExt.StatEffectGroupGUI("On-Scheduled Effect", atk.scheduledEffects, StatEffectContext.Action, ""+atk.GetInstanceID(), formulaOptions, lastFocusedControl);
		}
		atk.applicationEffects = EditorGUIExt.StatEffectGroupGUI("Per-Application Effect", atk.applicationEffects, StatEffectContext.Action, ""+atk.GetInstanceID(), formulaOptions, lastFocusedControl);
		atk.targetEffects = EditorGUIExt.StatEffectGroupsGUI("Application Effect Group", atk.targetEffects, StatEffectContext.Action, ""+atk.GetInstanceID(), formulaOptions, lastFocusedControl);
	}

	public override void OnSRPGCKInspectorGUI () {
		BasicSkillGUI();
		EditorGUILayout.Space();
		atk.io = EditorGUIExt.PickAssetGUI<SkillIO>("I/O", atk.io);
		EditorGUILayout.Space();
		TargetedSkillGUI();
		EditorGUILayout.Space();
		EffectSkillGUI();
		EditorGUILayout.Space();
		ReactionSkillGUI();
		EditorGUILayout.Space();
	}
}
