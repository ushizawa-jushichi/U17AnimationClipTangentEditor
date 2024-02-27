using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

#nullable enable

namespace Uchuhikoshi.AnimationClipTangentConverter
{
	public class AnimationClipTangentConverterTreeView : TreeView
	{
		IReadOnlyList<AnimationClipTangentConverterProcessor.ClipData>? _clipData = null!;

		static readonly string[] _mainModeOptionsWithMixed = {
			"ClampedAuto", "Auto(Obsolete)", "FreeSmooth", "Flat", "Broken", "(Mixed)", "---",
		};
		static readonly string[] _subModeOptionsWithMixed = {
			"Free", "Linear", "Constant", "(Mixed)", "---",
		};
		static readonly string[] _weightedOptionsWithMixed = {
			"False", "True", "(Mixed)", "---",
		};

		const int MainModeMixed = 5;
		const int MainModeZzz = 6;
		const int SubModeMixed = 3;
		const int SubModeZzz = 4;
		const int WeightedMixed = 2;
		const int WeightedZzz = 3;

		public Action? onChanged;
		bool _changeCheck;

		static readonly List<int> _expandsBackup = new();

		public AnimationClipTangentConverterTreeView(TreeViewState state, IReadOnlyList<AnimationClipTangentConverterProcessor.ClipData> clipData) : base(state, CreateHeader())
		{
			_clipData = clipData;
			Reload();
		}

		static MultiColumnHeader CreateHeader()
		{
			var columns = new[]
			{
				new MultiColumnHeaderState.Column { headerContent = new GUIContent("Clip"), width = 400, },

				new MultiColumnHeaderState.Column { headerContent = new GUIContent("Mode"), width = 120, },
				new MultiColumnHeaderState.Column { headerContent = new GUIContent("Both"), width = 90, },
				new MultiColumnHeaderState.Column { headerContent = new GUIContent("Left"), width = 90, },
				new MultiColumnHeaderState.Column { headerContent = new GUIContent("Right"), width = 90, },
				new MultiColumnHeaderState.Column { headerContent = new GUIContent("Weight.L"), width = 70, },
				new MultiColumnHeaderState.Column { headerContent = new GUIContent("Weight.R"), width = 70, },

				new MultiColumnHeaderState.Column { headerContent = new GUIContent("inTangent"), width = 80, },
				new MultiColumnHeaderState.Column { headerContent = new GUIContent("outTangent"), width = 80, },
				new MultiColumnHeaderState.Column { headerContent = new GUIContent("inWeight"), width = 80, },
				new MultiColumnHeaderState.Column { headerContent = new GUIContent("outWeight"), width = 80, },
			};
			var state = new MultiColumnHeaderState(columns);
			return new MultiColumnHeader(state);
		}

		protected override TreeViewItem BuildRoot()
		{
			var root = new TreeViewItem { id = 0, depth = -1, displayName = "Root" };
			root.children = new List<TreeViewItem>();

			var items = new List<TreeViewItem>();
			int id = 0;

			_expandsBackup.Clear();
			_expandsBackup.AddRange(GetExpanded());
			var expands = new List<int>();

			if (_clipData != null)
			{
				for (int ai = 0; ai < _clipData.Count; ai++)
				{
					var clipData = _clipData[ai];
					items.Add(new Item
					{
						id = ++id,
						depth = 0,
						displayName = (clipData.animationClip != null) ? clipData.animationClip.name : "(Null)",
						clipData = clipData,
						nodeData = null,
						curveData = null,
					});
					expands.Add(id);

					for (int ni = 0; ni < clipData.nodeData.Count; ni++)
					{
						var nodeData = clipData.nodeData[ni];
						var curveData0 = nodeData.curveData[0];
						int depthNode = 1;

						if (!nodeData.scalar)
						{
							items.Add(new Item
							{
								id = ++id,
								depth = depthNode,
								displayName = nodeData.displayLabel,
								clipData = null,
								nodeData = nodeData,
								curveData = null,
								keyData = null,
							});
							for (int ci = 0; ci < nodeData.curveData.Count; ci++)
							{
								var curveData = nodeData.curveData[ci];
								items.Add(new Item
								{
									id = ++id,
									depth = depthNode + 1,
									displayName = curveData.displayLabel,
									clipData = null,
									nodeData = null,
									curveData = curveData,
									keyData = null,
								});
								for (int ki = 0; ki < curveData.keyData.Count; ki++)
								{
									var keyData = curveData.keyData[ki];
									items.Add(new Item
									{
										id = ++id,
										depth = depthNode + 2,
										displayName = keyData.displayLabel,
										clipData = null,
										nodeData = null,
										curveData = null,
										keyData = keyData,
									});
								}
							}
						}
						else
						{
							items.Add(new Item
							{
								id = ++id,
								depth = depthNode,
								displayName = nodeData.displayLabel,
								clipData = null,
								nodeData = null,
								curveData = curveData0,
								keyData = null,
							});

							for (int ki = 0; ki < curveData0.keyData.Count; ki++)
							{
								var keyData = curveData0.keyData[ki];
								items.Add(new Item
								{
									id = ++id,
									depth = depthNode + 1,
									displayName = keyData.displayLabel,
									clipData = null,
									nodeData = null,
									curveData = null,
									keyData = keyData,
								});
							}
						}
					}
				}
			}

			SetupParentsAndChildrenFromDepths(root, items);

			if (expands.Count > 0) 
			{
				SetExpanded(expands);
			}
			if (_expandsBackup.Count > 0) 
			{
				SetExpanded(_expandsBackup);
				_expandsBackup.Clear();
			}
			return root;
		}

		public void Rebuild(IReadOnlyList<AnimationClipTangentConverterProcessor.ClipData> clipData)
		{
			_clipData = clipData;
			BuildRoot();
		}

		public void Clear()
		{
			_clipData = null;
			BuildRoot();
		}

		protected override void RowGUI(RowGUIArgs args)
		{
			var item = (Item)args.item;

			_changeCheck = false;

			if (item.curveData != null)
			{
				CheckUpdateCurve(item.curveData);
			}
			if (item.clipData != null)
			{
				CheckUpdateClip(item.clipData);
			}
			if (item.nodeData != null)
			{
				CheckUpdateNode(item.nodeData);
			}

			int mainModeChange = MainModeZzz;
			int subModeLeftChange = SubModeZzz;
			int subModeRightChange = SubModeZzz;
			int weightedLeftChange = WeightedZzz;
			int weightedRightChange = WeightedZzz;
			int indent = 0;

			if (item.clipData != null)
			{
				mainModeChange = item.clipData.modeChange.mainMode;
				subModeLeftChange = item.clipData.modeChange.subModeLeft;
				subModeRightChange = item.clipData.modeChange.subModeRight;
				weightedLeftChange = item.clipData.modeChange.weightedLeft;
				weightedRightChange = item.clipData.modeChange.weightedRight;
			}
			if (item.nodeData != null)
			{
				mainModeChange = item.nodeData.modeChange.mainMode;
				subModeLeftChange = item.nodeData.modeChange.subModeLeft;
				subModeRightChange = item.nodeData.modeChange.subModeRight;
				weightedLeftChange = item.nodeData.modeChange.weightedLeft;
				weightedRightChange = item.nodeData.modeChange.weightedRight;
				indent += item.nodeData.curveData[0].paths.Length - 1;
			}
			if (item.curveData != null)
			{
				mainModeChange = item.curveData.modeChange.mainMode;
				subModeLeftChange = item.curveData.modeChange.subModeLeft;
				subModeRightChange = item.curveData.modeChange.subModeRight;
				weightedLeftChange = item.curveData.modeChange.weightedLeft;
				weightedRightChange = item.curveData.modeChange.weightedRight;
				indent += item.curveData.paths.Length - 1;
			}
			if (item.keyData != null)
			{
				mainModeChange = item.keyData.modeChange.mainMode;
				subModeLeftChange = item.keyData.modeChange.subModeLeft;
				subModeRightChange = item.keyData.modeChange.subModeRight;
				weightedLeftChange = item.keyData.modeChange.weightedLeft;
				weightedRightChange = item.keyData.modeChange.weightedRight;
				indent += item.keyData.indent;
			}

			int subModeBothChange = SubModeZzz;
			if (mainModeChange != (int)AnimationClipTangentConverterProcessor.CurveMainMode.Broken)
			{
				if (subModeLeftChange != SubModeMixed)
				{
					subModeLeftChange = SubModeZzz;
				}
				if (subModeRightChange != SubModeMixed)
				{
					subModeRightChange = SubModeZzz;
				}
			}
			else
			{
				if (mainModeChange != MainModeMixed)
				{
					mainModeChange = SubModeZzz;
				}

				if (subModeLeftChange == subModeRightChange)
				{
					subModeBothChange = subModeLeftChange;

					if (subModeLeftChange != SubModeMixed)
					{
						subModeLeftChange = SubModeZzz;
					}
					if (subModeRightChange != SubModeMixed)
					{
						subModeRightChange = SubModeZzz;
					}
				}
			}

			for (var i = 0; i < args.GetNumVisibleColumns(); ++i)
			{
				var rect = args.GetCellRect(i);
				var columnIndex = args.GetColumn(i);

				switch (columnIndex)
				{
					case 0:
						rect.xMin += GetContentIndent(item);
						EditorGUI.indentLevel += indent;
						EditorGUI.LabelField(rect, item.displayName);
						EditorGUI.indentLevel -= indent;
						break;

					case 1:
						EditorGUI.BeginChangeCheck();
						mainModeChange = EditorGUI.Popup(rect, mainModeChange, _mainModeOptionsWithMixed);
						if (EditorGUI.EndChangeCheck() && (mainModeChange < MainModeMixed))
						{
							OnChangedMainMode(item, mainModeChange);
						}
						break;

					case 2:
						EditorGUI.BeginChangeCheck();
						subModeBothChange = EditorGUI.Popup(rect, subModeBothChange, _subModeOptionsWithMixed);
						if (EditorGUI.EndChangeCheck() && (subModeBothChange < SubModeMixed))
						{
							OnChangedSubMode(item, subModeBothChange, 2);
						}
						break;
					case 3:
						EditorGUI.BeginChangeCheck();
						subModeLeftChange = EditorGUI.Popup(rect, subModeLeftChange, _subModeOptionsWithMixed);
						if (EditorGUI.EndChangeCheck() && (subModeLeftChange < SubModeMixed))
						{
							OnChangedSubMode(item, subModeLeftChange, 0);
						}
						break;
					case 4:
						EditorGUI.BeginChangeCheck();
						subModeRightChange = EditorGUI.Popup(rect, subModeRightChange, _subModeOptionsWithMixed);
						if (EditorGUI.EndChangeCheck() && (subModeRightChange < SubModeMixed))
						{
							OnChangedSubMode(item, subModeRightChange, 1);
						}
						break;

					case 5:
						EditorGUI.BeginChangeCheck();
						weightedLeftChange = EditorGUI.Popup(rect, weightedLeftChange, _weightedOptionsWithMixed);
						if (EditorGUI.EndChangeCheck() && (weightedLeftChange < WeightedMixed))
						{
							OnChangedWeighted(item, weightedLeftChange, 0);
						}
						break;
					case 6:
						EditorGUI.BeginChangeCheck();
						weightedRightChange = EditorGUI.Popup(rect, weightedRightChange, _weightedOptionsWithMixed);
						if (EditorGUI.EndChangeCheck() && (weightedRightChange < WeightedMixed))
						{
							OnChangedWeighted(item, weightedRightChange, 1);
						}
						break;


					case 7:
						if (item.keyData != null)
						{
							EditorGUI.LabelField(rect, item.keyData.key.inTangent.ToString());
						}
						break;
					case 8:
						if (item.keyData != null)
						{
							EditorGUI.LabelField(rect, item.keyData.key.outTangent.ToString());
						}
						break;
					case 9:
						if (item.keyData != null)
						{
							EditorGUI.LabelField(rect, item.keyData.key.inWeight.ToString());
						}
						break;
					case 10:
						if (item.keyData != null)
						{
							EditorGUI.LabelField(rect, item.keyData.key.outWeight.ToString());
						}
						break;
				}
			}
	
			if (_changeCheck)
			{
				_changeCheck = false;
				onChanged?.Invoke();
			}
		}

		void CheckUpdateClip(AnimationClipTangentConverterProcessor.ClipData clipData)
		{
			var nodeData0 = clipData.nodeData[0];
			bool mainModeEqualsAll = (nodeData0.modeChange.mainMode < MainModeMixed);
			bool subModeLeftEqualsAll = (nodeData0.modeChange.subModeLeft < SubModeMixed);
			bool subModeRightEqualsAll = (nodeData0.modeChange.subModeRight < SubModeMixed);
			bool weightedLeftEqualsAll = (nodeData0.modeChange.weightedLeft < WeightedMixed);
			bool weightedRightEqualsAll = (nodeData0.modeChange.weightedRight < WeightedMixed);

			for (int i = 1; i < clipData.nodeData.Count; i++)
			{
				var nodeData = clipData.nodeData[i];
				CheckUpdateNode(nodeData);

				if (nodeData.modeChange.mainMode != nodeData0.modeChange.mainMode && (nodeData.modeChange.mainMode < MainModeMixed))
				{
					clipData.modeChange.mainMode = MainModeMixed;
					mainModeEqualsAll = false;
				}
				if (nodeData.modeChange.subModeLeft != nodeData0.modeChange.subModeLeft && (nodeData.modeChange.subModeLeft < SubModeMixed))
				{
					clipData.modeChange.subModeLeft = SubModeMixed;
					subModeLeftEqualsAll = false;
				}
				if (nodeData.modeChange.subModeRight != nodeData0.modeChange.subModeRight && (nodeData.modeChange.subModeRight < SubModeMixed))
				{
					clipData.modeChange.subModeRight = SubModeMixed;
					subModeRightEqualsAll = false;
				}
				if (nodeData.modeChange.weightedLeft != nodeData0.modeChange.weightedLeft && (nodeData.modeChange.weightedLeft < WeightedMixed))
				{
					clipData.modeChange.weightedLeft = WeightedMixed;
					weightedLeftEqualsAll = false;
				}
				if (nodeData.modeChange.weightedRight != nodeData0.modeChange.weightedRight && (nodeData.modeChange.weightedRight < WeightedMixed))
				{
					clipData.modeChange.weightedRight = WeightedMixed;
					weightedRightEqualsAll = false;
				}
			}
			if (mainModeEqualsAll)
			{
				clipData.modeChange.mainMode = nodeData0.modeChange.mainMode;
			}
			if (subModeLeftEqualsAll)
			{
				clipData.modeChange.subModeLeft = nodeData0.modeChange.subModeLeft;
			}
			if (subModeRightEqualsAll)
			{
				clipData.modeChange.subModeRight = nodeData0.modeChange.subModeRight;
			}
			if (weightedLeftEqualsAll)
			{
				clipData.modeChange.weightedLeft = nodeData0.modeChange.weightedLeft;
			}
			if (weightedRightEqualsAll)
			{
				clipData.modeChange.weightedRight = nodeData0.modeChange.weightedRight;
			}
		}

		void CheckUpdateNode(AnimationClipTangentConverterProcessor.NodeData nodeData)
		{
			var curveData0 = nodeData.curveData[0];
			bool mainModeEqualsAll = (curveData0.modeChange.mainMode < MainModeMixed);
			bool subModeLeftEqualsAll = (curveData0.modeChange.subModeLeft < SubModeMixed);
			bool subModeRightEqualsAll = (curveData0.modeChange.subModeRight < SubModeMixed);
			bool weightedLeftEqualsAll = (curveData0.modeChange.weightedLeft < WeightedMixed);
			bool weightedRightEqualsAll = (curveData0.modeChange.weightedRight < WeightedMixed);

			for (int i = 1; i < nodeData.curveData.Count; i++)
			{
				var curveData = nodeData.curveData[i];
				CheckUpdateCurve(curveData);

				if (curveData.modeChange.mainMode != curveData0.modeChange.mainMode && (curveData.modeChange.mainMode < MainModeMixed))
				{
					nodeData.modeChange.mainMode = MainModeMixed;
					mainModeEqualsAll = false;
				}
				if (curveData.modeChange.subModeLeft != curveData0.modeChange.subModeLeft && (curveData.modeChange.subModeLeft < SubModeMixed))
				{
					nodeData.modeChange.subModeLeft = SubModeMixed;
					subModeLeftEqualsAll = false;
				}
				if (curveData.modeChange.subModeRight != curveData0.modeChange.subModeRight && (curveData.modeChange.subModeRight < SubModeMixed))
				{
					nodeData.modeChange.subModeRight = SubModeMixed;
					subModeRightEqualsAll = false;
				}
				if (curveData.modeChange.weightedLeft != curveData0.modeChange.weightedLeft && (curveData.modeChange.weightedLeft < WeightedMixed))
				{
					nodeData.modeChange.weightedLeft = WeightedMixed;
					weightedLeftEqualsAll = false;
				}
				if (curveData.modeChange.weightedRight != curveData0.modeChange.weightedRight && (curveData.modeChange.weightedRight < WeightedMixed))
				{
					nodeData.modeChange.weightedRight = WeightedMixed;
					weightedRightEqualsAll = false;
				}
			}
			if (mainModeEqualsAll)
			{
				nodeData.modeChange.mainMode = curveData0.modeChange.mainMode;
			}
			if (subModeLeftEqualsAll)
			{
				nodeData.modeChange.subModeLeft = curveData0.modeChange.subModeLeft;
			}
			if (subModeRightEqualsAll)
			{
				nodeData.modeChange.subModeRight = curveData0.modeChange.subModeRight;
			}
			if (weightedLeftEqualsAll)
			{
				nodeData.modeChange.weightedLeft = curveData0.modeChange.weightedLeft;
			}
			if (weightedRightEqualsAll)
			{
				nodeData.modeChange.weightedRight = curveData0.modeChange.weightedRight;
			}
		}

		void CheckUpdateCurve(AnimationClipTangentConverterProcessor.CurveData curveData)
		{
			var keyData0 = curveData.keyData[0];
			bool mainModeEqualsAll = (keyData0.modeChange.mainMode < MainModeMixed);
			bool subModeLeftEqualsAll = (keyData0.modeChange.subModeLeft < SubModeMixed);
			bool subModeRightEqualsAll = (keyData0.modeChange.subModeRight < SubModeMixed);
			bool weightedLeftEqualsAll = (keyData0.modeChange.weightedLeft < WeightedMixed);
			bool weightedRightEqualsAll = (keyData0.modeChange.weightedRight < WeightedMixed);

			for (int i = 1; i < curveData.keyData.Count; i++)
			{
				var keyData = curveData.keyData[i];
				if (keyData.modeChange.mainMode != keyData0.modeChange.mainMode && (keyData.modeChange.mainMode < MainModeMixed))
				{
					curveData.modeChange.mainMode = MainModeMixed;
					mainModeEqualsAll = false;
				}
				if (keyData.modeChange.subModeLeft != keyData0.modeChange.subModeLeft && (keyData.modeChange.subModeLeft < SubModeMixed))
				{
					curveData.modeChange.subModeLeft = SubModeMixed;
					subModeLeftEqualsAll = false;
				}
				if (keyData.modeChange.subModeRight != keyData0.modeChange.subModeRight && (keyData.modeChange.subModeRight < SubModeMixed))
				{
					curveData.modeChange.subModeRight = SubModeMixed;
					subModeRightEqualsAll = false;
				}
				if (keyData.modeChange.weightedLeft != keyData0.modeChange.weightedLeft && (keyData.modeChange.weightedLeft < WeightedMixed))
				{
					curveData.modeChange.weightedLeft = WeightedMixed;
					weightedLeftEqualsAll = false;
				}
				if (keyData.modeChange.weightedRight != keyData0.modeChange.weightedRight && (keyData.modeChange.weightedRight < WeightedMixed))
				{
					curveData.modeChange.weightedRight = WeightedMixed;
					weightedRightEqualsAll = false;
				}
			}
			if (mainModeEqualsAll)
			{
				curveData.modeChange.mainMode = keyData0.modeChange.mainMode;
			}
			if (subModeLeftEqualsAll)
			{
				curveData.modeChange.subModeLeft = keyData0.modeChange.subModeLeft;
			}
			if (subModeRightEqualsAll)
			{
				curveData.modeChange.subModeRight = keyData0.modeChange.subModeRight;
			}
			if (weightedLeftEqualsAll)
			{
				curveData.modeChange.weightedLeft = keyData0.modeChange.weightedLeft;
			}
			if (weightedRightEqualsAll)
			{
				curveData.modeChange.weightedRight = keyData0.modeChange.weightedRight;
			}
		}

		void OnChangedMainMode(Item item, int value)
		{
			_changeCheck = true;

			if (item.clipData != null)
			{
				OnChangedMainMode(item.clipData, value);
			}
			if (item.nodeData != null)
			{
				OnChangedMainMode(item.nodeData, value);
			}
			if (item.curveData != null)
			{
				OnChangedMainMode(item.curveData, value);
			}
			if (item.keyData != null)
			{
				OnChangedMainMode(item.keyData, value);
			}
		}

		void OnChangedMainMode(AnimationClipTangentConverterProcessor.ClipData clipData, int value)
		{
			_changeCheck = true;
			clipData.modeChange.mainMode = value;
			foreach (var nodeData in clipData.nodeData)
			{
				OnChangedMainMode(nodeData, value);
			}
		}

		void OnChangedMainMode(AnimationClipTangentConverterProcessor.NodeData nodeData, int value)
		{
			_changeCheck = true;
			nodeData.modeChange.mainMode = value;
			foreach (var curveData in nodeData.curveData)
			{
				OnChangedMainMode(curveData, value);
			}
		}

		void OnChangedMainMode(AnimationClipTangentConverterProcessor.CurveData curveData, int value)
		{
			_changeCheck = true;
			curveData.modeChange.mainMode = value;
			foreach (var keyData in curveData.keyData)
			{
				OnChangedMainMode(keyData, value);
			}
		}

		void OnChangedMainMode(AnimationClipTangentConverterProcessor.KeyData keyData, int value)
		{
			_changeCheck = true;
			keyData.modeChange.mainMode = value;
		}

		void OnChangedSubMode(Item item, int value, int mode)
		{
			_changeCheck = true;

			if (item.clipData != null)
			{
				OnChangedSubMode(item.clipData, value, mode);
			}
			if (item.nodeData != null)
			{
				OnChangedSubMode(item.nodeData, value, mode);
			}
			if (item.curveData != null)
			{
				OnChangedSubMode(item.curveData, value, mode);
			}
			if (item.keyData != null)
			{
				OnChangedSubMode(item.keyData, value, mode);
			}
		}

		void OnChangedSubMode(AnimationClipTangentConverterProcessor.ClipData clipData, int value, int mode)
		{
			_changeCheck = true;
			if (mode == 0 || mode == 2)
			{
				clipData.modeChange.subModeLeft = value;
			}
			if (mode == 1 || mode == 2)
			{
				clipData.modeChange.subModeRight = value;
			}
			clipData.modeChange.mainMode = (int)AnimationClipTangentConverterProcessor.CurveMainMode.Broken;

			foreach (var nodeData in clipData.nodeData)
			{
				OnChangedSubMode(nodeData, value, mode);
			}
		}

		void OnChangedSubMode(AnimationClipTangentConverterProcessor.NodeData nodeData, int value, int mode)
		{
			_changeCheck = true;
			if (mode == 0 || mode == 2)
			{
				nodeData.modeChange.subModeLeft = value;
			}
			if (mode == 1 || mode == 2)
			{
				nodeData.modeChange.subModeRight = value;
			}
			nodeData.modeChange.mainMode = (int)AnimationClipTangentConverterProcessor.CurveMainMode.Broken;

			foreach (var curveData in nodeData.curveData)
			{
				OnChangedSubMode(curveData, value, mode);
			}
		}

		void OnChangedSubMode(AnimationClipTangentConverterProcessor.CurveData curveData, int value, int mode)
		{
			_changeCheck = true;
			if (mode == 0 || mode == 2)
			{
				curveData.modeChange.subModeLeft = value;
			}
			if (mode == 1 || mode == 2)
			{
				curveData.modeChange.subModeRight = value;
			}
			curveData.modeChange.mainMode = (int)AnimationClipTangentConverterProcessor.CurveMainMode.Broken;

			foreach (var keyData in curveData.keyData)
			{
				OnChangedSubMode(keyData, value, mode);
			}
		}

		void OnChangedSubMode(AnimationClipTangentConverterProcessor.KeyData keyData, int value, int mode)
		{
			_changeCheck = true;
			if (mode == 0 || mode == 2)
			{
				keyData.modeChange.subModeLeft = value;
			}
			if (mode == 1 || mode == 2)
			{
				keyData.modeChange.subModeRight = value;
			}
			keyData.modeChange.mainMode = (int)AnimationClipTangentConverterProcessor.CurveMainMode.Broken;
		}

		void OnChangedWeighted(Item item, int value, int mode)
		{
			_changeCheck = true;

			if (item.clipData != null)
			{
				OnChangedWeighted(item.clipData, value, mode);
			}
			if (item.nodeData != null)
			{
				OnChangedWeighted(item.nodeData, value, mode);
			}
			if (item.curveData != null)
			{
				OnChangedWeighted(item.curveData, value, mode);
			}
			if (item.keyData != null)
			{
				OnChangedWeighted(item.keyData, value, mode);
			}
		}

		void OnChangedWeighted(AnimationClipTangentConverterProcessor.ClipData clipData, int value, int mode)
		{
			_changeCheck = true;
			if (mode == 0)
			{
				clipData.modeChange.weightedLeft = value;
			}
			else if (mode == 1)
			{
				clipData.modeChange.weightedRight = value;
			}
			foreach (var nodeData in clipData.nodeData)
			{
				OnChangedWeighted(nodeData, value, mode);
			}
		}

		void OnChangedWeighted(AnimationClipTangentConverterProcessor.NodeData nodeData, int value, int mode)
		{
			_changeCheck = true;
			if (mode == 0)
			{
				nodeData.modeChange.weightedLeft = value;
			}
			else if (mode == 1)
			{
				nodeData.modeChange.weightedRight = value;
			}
			foreach (var curveData in nodeData.curveData)
			{
				OnChangedWeighted(curveData, value, mode);
			}
		}

		void OnChangedWeighted(AnimationClipTangentConverterProcessor.CurveData curveData, int value, int mode)
		{
			_changeCheck = true;
			if (mode == 0)
			{
				curveData.modeChange.weightedLeft = value;
			}
			else if (mode == 1)
			{
				curveData.modeChange.weightedRight = value;
			}
			foreach (var keyData in curveData.keyData)
			{
				OnChangedWeighted(keyData, value, mode);
			}
		}

		void OnChangedWeighted(AnimationClipTangentConverterProcessor.KeyData keyData, int value, int mode)
		{
			_changeCheck = true;
			if (mode == 0)
			{
				keyData.modeChange.weightedLeft = value;
			}
			else if (mode == 1)
			{
				keyData.modeChange.weightedRight = value;
			}
		}

		class Item : TreeViewItem
		{
			public AnimationClipTangentConverterProcessor.ClipData? clipData;
			public AnimationClipTangentConverterProcessor.NodeData? nodeData;
			public AnimationClipTangentConverterProcessor.CurveData? curveData;
			public AnimationClipTangentConverterProcessor.KeyData? keyData;
		}
	}
}