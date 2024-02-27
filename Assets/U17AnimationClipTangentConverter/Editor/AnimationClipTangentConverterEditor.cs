using UnityEditor;
using UnityEngine;

#nullable enable

namespace Uchuhikoshi.AnimationClipTangentConverter
{
	[CustomEditor(typeof(AnimationClipTangentConverter))]
	public sealed class AnimationClipTangentConverterEditor : Editor
	{
		bool _hasError = false;
		bool _modified = false;
		AnimationClipTangentConverterProcessor _convertProcessor = new();

		void OnEnable()
		{
		}

		void OnDestroy()
		{
			AnimationClipTangentConverterWindow.DestroyInstance();
		}

		public override void OnInspectorGUI()
		{
			serializedObject.Update();

			AnimationClipTangentConverter settings = (AnimationClipTangentConverter)target;

			EditorGUI.BeginChangeCheck();
			base.OnInspectorGUI();
			if (EditorGUI.EndChangeCheck())
			{
				Validate();
			}
			if (_hasError)
			{
				EditorGUILayout.HelpBox("Cannot convert sub-asset AnimationClip.", MessageType.Error);
			}

			if (GUILayout.Button("Show AnimationCurve List", GUILayout.Width(240)))
			{
				_convertProcessor.Reload(settings.targetAnimationClips);
				OnLoaded();
				_modified = false;
			}

			serializedObject.ApplyModifiedProperties();
		}

		void Validate()
		{
			_hasError = false;
			AnimationClipTangentConverter settings = (AnimationClipTangentConverter)target;
			foreach (var clip in settings.targetAnimationClips)
			{
				if (clip != null && AssetDatabase.IsSubAsset(clip.GetInstanceID()))
				{
					_hasError = true;
				}
			}
		}

		void OnLoaded()
		{
			if (!AnimationClipTangentConverterWindow.isValid)
			{
				AnimationClipTangentConverterWindow.CreateInstance(_convertProcessor.clipData);
				AnimationClipTangentConverterWindow.instance.onChanged += () =>
				{
					if (!_modified)
					{
						_modified = true;
						Repaint();
					}
				};
				AnimationClipTangentConverterWindow.instance.onClickConvert += () =>
				{
					if (_modified)
					{
						_convertProcessor.ProcessConvert();
						_modified = false;
					}
				};
			}
			else
			{
				AnimationClipTangentConverterWindow.instance.Rebuild(_convertProcessor.clipData);
			}
		}
	}
}
