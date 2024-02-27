using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

#nullable enable

namespace Uchuhikoshi.AnimationClipTangentConverter
{
	public sealed class AnimationClipTangentConverterWindow : EditorWindow
	{
		TreeViewState _treeViewState = null!;
		AnimationClipTangentConverterTreeView _treeView = null!;
		SearchField _searchField = null!;

		public Action? onChanged;
		public Action? onClickConvert;
		bool _canConvert;

		static AnimationClipTangentConverterWindow? _instance;
		public static bool isValid => _instance != null;
		public static AnimationClipTangentConverterWindow instance
		{
			get
			{
				if (_instance == null)
				{
					throw new InvalidOperationException();
				}
				return _instance!;
			}
		}

		public static AnimationClipTangentConverterWindow CreateInstance(IReadOnlyList<AnimationClipTangentConverterProcessor.ClipData> clipData)
		{
			_instance = GetWindow<AnimationClipTangentConverterWindow>();
			_instance.Show();
			_instance._treeViewState = new TreeViewState();
			_instance._treeView = new AnimationClipTangentConverterTreeView(_instance._treeViewState, clipData);
			_instance._searchField = new SearchField();
			_instance._treeView.onChanged = () =>
			{
				_instance._canConvert = true;
				_instance.onChanged?.Invoke();
			};
			return _instance;
		}

		public static void DestroyInstance()
		{
			if (_instance != null)
			{
				DestroyImmediate(_instance);
				_instance = null;
			}
		}

		void OnGUI()
		{
			if (_searchField == null || _treeView == null)
				return;

			EditorGUILayout.BeginHorizontal();
			EditorGUI.BeginDisabledGroup(!_canConvert);
			if (GUILayout.Button("Convert", GUILayout.Width(90)))
			{
				onClickConvert?.Invoke();
				_canConvert = false;
			}
			EditorGUI.EndDisabledGroup();
			var searchRect = EditorGUILayout.GetControlRect(false, GUILayout.ExpandWidth(true), GUILayout.Height(EditorGUIUtility.singleLineHeight));
			_treeView.searchString = _searchField.OnGUI(searchRect, _treeView.searchString);
			EditorGUILayout.EndHorizontal();

			var treeViewRect = EditorGUILayout.GetControlRect(false, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
			_treeView.OnGUI(treeViewRect);
		}

		public void Rebuild(IReadOnlyList<AnimationClipTangentConverterProcessor.ClipData> clipData)
		{
			_treeView?.Rebuild(clipData);
		}
	}
}
