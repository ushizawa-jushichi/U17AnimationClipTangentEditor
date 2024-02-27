using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

#nullable enable

namespace Uchuhikoshi.AnimationClipTangentConverter
{
	public class AnimationClipTangentConverterProcessor
	{
		readonly List<ClipData> _clipData = new List<ClipData>();
		public IReadOnlyList<ClipData> clipData => _clipData;

		const int MainModeMixed = 5;
		const int MainModeInit = 6;
		const int SubModeMixed = 3;
		const int SubModeInit = 4;
		const int WeightedMixed = 2;
		const int WeightedInit = 3;

		public enum CurveMainMode
		{
			ClampedAuto = 0,
			Auto,
			FreeSmooth,
			Flat,
			Broken,
		};
		public enum CurveSubMode
		{
			Free = 0,
			Linear,
			Constant,
		};

		public class CurveMode
		{
			public int mainMode;
			public int subModeLeft;
			public int subModeRight;
			public int weightedLeft;
			public int weightedRight;

			public CurveMode()
			{
			}

			public CurveMode(int mainMode, int subModeLeft, int subModeRight, int weightedLeft, int weightedRight)
			{
				this.mainMode = mainMode;
				this.subModeLeft = subModeLeft;
				this.subModeRight = subModeRight;
				this.weightedLeft = weightedLeft;
				this.weightedRight = weightedRight;
			}

			public void CopyFrom(CurveMode other)
			{
				this.mainMode = other.mainMode;
				this.subModeLeft = other.subModeLeft;
				this.subModeRight = other.subModeRight;
				this.weightedLeft = other.weightedLeft;
				this.weightedRight = other.weightedRight;
			}

			public bool Equals(CurveMode other) =>
				mainMode == other.mainMode &&
				subModeLeft == other.subModeLeft &&
				subModeRight == other.subModeRight &&
				weightedLeft == other.weightedLeft &&
				weightedRight == other.weightedRight;
		}

		public class ClipData
		{
			public AnimationClip? animationClip;

			public List<NodeData> nodeData = new List<NodeData>();
			public List<CurveData> curveData = new List<CurveData>();
			public CurveMode modeChange = new();
			public CurveMode modeCurrent = new CurveMode();
		}

		public class NodeData
		{
			public List<CurveData> curveData = new List<CurveData>();
			public CurveMode modeChange = new();
			public CurveMode modeCurrent = new CurveMode();

			public string displayLabel = string.Empty;
			public string nodeKey = string.Empty;
			public bool scalar;
		}

		public class CurveData
		{
			public AnimationCurve? curve;
			public EditorCurveBinding binding;
			public List<KeyData> keyData = new List<KeyData>();
			public CurveMode modeChange = new();
			public CurveMode modeCurrent = new CurveMode();

			public string displayLabel = string.Empty;
			public string friendlyName = string.Empty;
			public string nodeKey = string.Empty;

			public string path = string.Empty;
			public string[] paths = new string[0];
			public string propertyName = string.Empty;
			public string[] props = new string[0];

			public bool scalar;
		}

		public class KeyData
		{
			public CurveMode modeChange = new();
			public CurveMode modeCurrent = new CurveMode();

			public string displayLabel = string.Empty;

			public int indent;
			public int index;
			public Keyframe key;
		}

		public void Reload(AnimationClip?[] targetAnimationClips)
		{
			_clipData.Clear();

			for (int ai = 0; ai < targetAnimationClips.Length; ai++)
			{
				var animationClip = targetAnimationClips[ai];
				if (animationClip == null)
				{
					continue;
				}
				var clipData = new ClipData();
				clipData.animationClip = animationClip;
				var curveBindings = AnimationUtility.GetCurveBindings(animationClip);

				foreach (var binding in curveBindings)
				{
					var curve = AnimationUtility.GetEditorCurve(animationClip, binding);

					var curveData = new CurveData();
					curveData.curve = curve;
					curveData.binding = binding;

					char[] seps2 = { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };
					curveData.path = binding.path;
					curveData.paths = binding.path.Split(seps2);
					char[] seps1 = { '.' };
					curveData.propertyName = binding.propertyName;
					curveData.props = binding.propertyName.Split(seps1);

					bool nodeKeyPathOnly = false;
					bool nodeScalar = false;

					curveData.friendlyName = curveData.propertyName;
					if (curveData.propertyName.StartsWith("blendShape.", StringComparison.Ordinal))
					{
						curveData.friendlyName = curveData.propertyName;
						curveData.scalar = true;
						nodeKeyPathOnly = true;
					}
					else if (curveData.propertyName.StartsWith("m_FocalLength", StringComparison.Ordinal))
					{
						curveData.friendlyName = "Camera.Focal Length";
						curveData.scalar = true;
						nodeScalar = true;
					}
					else if (curveData.propertyName.StartsWith("m_FocusDistance", StringComparison.Ordinal))
					{
						curveData.friendlyName = "Camera.Focus Distance";
						curveData.scalar = true;
						nodeScalar = true;
					}
					else if (curveData.propertyName.StartsWith("m_SensorSize.", StringComparison.Ordinal))
					{
						curveData.friendlyName = "Sensor Size";
					}
					else if (curveData.propertyName.StartsWith("m_LocalRotation.", StringComparison.Ordinal))
					{
						curveData.friendlyName = "Rotation";
					}
					else if (curveData.propertyName.StartsWith("m_LocalPosition.", StringComparison.Ordinal))
					{
						curveData.friendlyName = "Position";
					}
					else if (curveData.propertyName.StartsWith("m_LocalScale.", StringComparison.Ordinal))
					{
						curveData.friendlyName = "Scale";
					}

					if (!curveData.scalar)
					{
						curveData.displayLabel = $"{curveData.friendlyName}.{curveData.props[^1]}";
					}
					else
					{
						curveData.displayLabel = $"{curveData.paths[^1]} : {curveData.friendlyName}";
					}

					curveData.nodeKey = nodeKeyPathOnly ? binding.path : $"{binding.path}/{curveData.friendlyName}";

					var nodeData = clipData.nodeData.FirstOrDefault(x => x.nodeKey.Equals(curveData.nodeKey));
					if (nodeData == null)
					{
						nodeData = new NodeData();
						nodeData.nodeKey = curveData.nodeKey;
						nodeData.displayLabel = nodeKeyPathOnly ? curveData.paths[^1] :
												(nodeScalar ? curveData.displayLabel : $"{curveData.paths[^1]} : {curveData.friendlyName}");
						nodeData.scalar = nodeScalar;
						clipData.nodeData.Add(nodeData);
					}

					nodeData.curveData.Add(curveData);
					clipData.curveData.Add(curveData);

					if (curve.keys.Length > 0)
					{
						AnimationUtility.TangentMode tanL = AnimationUtility.TangentMode.Free;
						AnimationUtility.TangentMode tanR = AnimationUtility.TangentMode.Free;
						for (int ki = 0; ki < curve.keys.Length; ki++)
						{
							tanL = AnimationUtility.GetKeyLeftTangentMode(curve, ki);
							tanR = AnimationUtility.GetKeyRightTangentMode(curve, ki);

							var keyData = new KeyData();
							keyData.key = curve.keys[ki];
							keyData.displayLabel = $"{ki}  :  {keyData.key.value}  :  {keyData.key.time}";
							keyData.indent = curveData.paths.Length;
							keyData.index = ki;
							bool broken = AnimationUtility.GetKeyBroken(curve, ki);
							DetectCurveMode(curve, ki, broken, tanL, tanR,
									out keyData.modeCurrent.mainMode,
									out keyData.modeCurrent.subModeLeft, out keyData.modeCurrent.subModeRight,
									out keyData.modeCurrent.weightedLeft, out keyData.modeCurrent.weightedRight);

							keyData.modeChange.CopyFrom(keyData.modeCurrent);
							curveData.keyData.Add(keyData);
						}
					}

					curveData.modeCurrent = new CurveMode(MainModeInit, SubModeInit, SubModeInit, WeightedInit, WeightedInit);
					if (curveData.keyData.Count > 0)
					{
						var keyData0 = curveData.keyData[0];
						curveData.modeCurrent.CopyFrom(keyData0.modeCurrent);
						for (int ki = 1; ki < curveData.keyData.Count; ki++)
						{
							CompareCurveMode(ref curveData.modeCurrent, curveData.keyData[ki].modeCurrent, keyData0.modeCurrent);
						}
					}
					curveData.modeChange.CopyFrom(curveData.modeCurrent);
				}

				foreach (var nodeData in clipData.nodeData)
				{
					if (nodeData.curveData.Count > 1)
					{
						nodeData.curveData.Sort((x, y) =>
						{
							return string.Compare(x.props[^1], 0, y.props[^1], 0, 100);
						});
					}
				}
				if (clipData.nodeData.Count > 1)
				{
					clipData.nodeData.Sort((x, y) =>
					{
						int r = x.curveData[0].paths.Length - y.curveData[0].paths.Length;
						if (r != 0)
						{
							return r * 10000;
						}
						var xtrs = IsPositionOrRotationOrScale(x.curveData[0].friendlyName);
						var ytrs = IsPositionOrRotationOrScale(y.curveData[0].friendlyName);
						if (xtrs != ytrs)
						{
							return xtrs ? -9999 : 9999;
						}
						return string.Compare(x.nodeKey, 0, y.nodeKey, 0, 100);
					});
				}

				foreach (var nodeData in clipData.nodeData)
				{
					nodeData.modeCurrent = new CurveMode(MainModeInit, SubModeInit, SubModeInit, WeightedInit, WeightedInit);
					if (nodeData.curveData.Count > 0)
					{
						var curveData0 = nodeData.curveData[0];
						nodeData.modeCurrent.CopyFrom(curveData0.modeCurrent);
						for (int ci = 1; ci < nodeData.curveData.Count; ci++)
						{
							CompareCurveMode(ref nodeData.modeCurrent, nodeData.curveData[ci].modeCurrent, curveData0.modeCurrent);
						}
					}
					nodeData.modeChange.CopyFrom(nodeData.modeCurrent);
				}

				if (clipData.nodeData.Count > 0)
				{
					clipData.modeCurrent = new CurveMode(MainModeInit, SubModeInit, SubModeInit, WeightedInit, WeightedInit);
					if (clipData.nodeData.Count > 0)
					{
						var nodeData0 = clipData.nodeData[0];
						clipData.modeCurrent.CopyFrom(nodeData0.modeCurrent);
						for (int ni = 1; ni < clipData.nodeData.Count; ni++)
						{
							CompareCurveMode(ref clipData.modeCurrent, clipData.nodeData[ni].modeCurrent, nodeData0.modeCurrent);
						}
					}
					clipData.modeChange.CopyFrom(clipData.modeCurrent);
				}

				_clipData.Add(clipData);
			}
		}

		bool IsPositionOrRotationOrScale(string str)
		{
			return str.Equals("Position") ||
					str.Equals("Rotation") ||
					str.Equals("Scale");
		}

		void CompareCurveMode(ref CurveMode modeTarget, CurveMode modeCmp, CurveMode mode0)
		{
			if (modeCmp.mainMode != mode0.mainMode)
			{
				modeTarget.mainMode = MainModeMixed;
			}
			if (modeCmp.subModeLeft != mode0.subModeLeft)
			{
				modeTarget.subModeLeft = SubModeMixed;
			}
			if (modeCmp.subModeRight != mode0.subModeRight)
			{
				modeTarget.subModeRight = SubModeMixed;
			}
			if (modeCmp.weightedLeft != mode0.weightedLeft)
			{
				modeTarget.weightedLeft = WeightedMixed;
			}
			if (modeCmp.weightedRight != mode0.weightedRight)
			{
				modeTarget.weightedRight = WeightedMixed;
			}
		}

		public void ProcessConvert()
		{
			int totalProgress = 0;
			for (int ai = 0; ai < _clipData.Count; ai++)
			{
				var clipData = _clipData[ai];
				for (int ni = 0; ni < clipData.nodeData.Count; ni++)
				{
					var nodeData = clipData.nodeData[ni];
					for (int ci = 0; ci < nodeData.curveData.Count; ci++)
					{
						var curveData = nodeData.curveData[ci];
						for (int ki = 0; ki < curveData.keyData.Count; ki++)
						{
							var keyData = curveData.keyData[ki];
							if (keyData.modeCurrent.Equals(keyData.modeChange))
							{
								continue;
							}
							totalProgress++;
						}
					}
				}
			}
			if (totalProgress == 0)
			{
				return;
			}

			int progress = 0;
			for (int ai = 0; ai < _clipData.Count; ai++)
			{
				var clipData = _clipData[ai];
				bool modified = false;
				for (int ni = 0; ni < clipData.nodeData.Count; ni++)
				{
					var nodeData = clipData.nodeData[ni];
					for (int ci = 0; ci < nodeData.curveData.Count; ci++)
					{
						var curveData = nodeData.curveData[ci];
						if (curveData.curve != null)
						{
							{
								for (int ki = 0; ki < curveData.curve.keys.Length; ki++)
								{
									var keyData = curveData.keyData[ki];
									if (keyData.modeCurrent.Equals(keyData.modeChange))
									{
										continue;
									}

									var key = curveData.curve.keys[ki];
									bool updateKeyFrame = false;

									AnimationUtility.TangentMode tanL = AnimationUtility.TangentMode.Free;
									AnimationUtility.TangentMode tanR = AnimationUtility.TangentMode.Free;
									bool broken = false;

									if (((CurveMainMode)keyData.modeChange.mainMode) == CurveMainMode.Broken)
									{
										broken = true;

										tanL = (keyData.modeChange.subModeLeft == (int)CurveSubMode.Linear) ? AnimationUtility.TangentMode.Linear :
												(keyData.modeChange.subModeLeft == (int)CurveSubMode.Constant) ? AnimationUtility.TangentMode.Constant : AnimationUtility.TangentMode.Free;

										tanR = (keyData.modeChange.subModeRight == (int)CurveSubMode.Linear) ? AnimationUtility.TangentMode.Linear :
												(keyData.modeChange.subModeRight == (int)CurveSubMode.Constant) ? AnimationUtility.TangentMode.Constant : AnimationUtility.TangentMode.Free;
										if ((tanL == AnimationUtility.TangentMode.Free) ||
											(tanR == AnimationUtility.TangentMode.Free))
										{
											updateKeyFrame = true;
											float slope = CalculateSmoothTangent(key);
											if (tanL == AnimationUtility.TangentMode.Free)
											{
												key.inTangent = slope;
												updateKeyFrame = true;
											}
											if (tanR == AnimationUtility.TangentMode.Free)
											{
												key.outTangent = slope;
												updateKeyFrame = true;
											}
										}

										if (tanL == AnimationUtility.TangentMode.Constant)
										{
											key.inTangent = Mathf.Infinity;
											updateKeyFrame = true;
										}
										if (tanR == AnimationUtility.TangentMode.Constant)
										{
											key.outTangent = Mathf.Infinity;
											updateKeyFrame = true;
										}
									}
									else
									{

										switch ((CurveMainMode)keyData.modeChange.mainMode)
										{
											case CurveMainMode.ClampedAuto:
												tanL = AnimationUtility.TangentMode.ClampedAuto;
												break;
											case CurveMainMode.Auto:
												tanL = AnimationUtility.TangentMode.Auto;
												break;
											case CurveMainMode.FreeSmooth:
												tanL = AnimationUtility.TangentMode.Free;
												break;
											case CurveMainMode.Flat:
												tanL = AnimationUtility.TangentMode.Free;
												key.inTangent = 0f;
												key.outTangent = 0f;
												updateKeyFrame = true;
												break;
										}
										tanR = tanL;
									}

									if (keyData.modeChange.weightedLeft == 1)
									{
										if ((tanL == AnimationUtility.TangentMode.Linear) ||
											(tanL == AnimationUtility.TangentMode.Constant))

										{
											tanL = AnimationUtility.TangentMode.Free;
										}
										if (ki > 0)
										{
											key.inWeight = 1 / 3.0f;
										}
									}
									if (keyData.modeChange.weightedRight == 1)
									{
										if ((tanR == AnimationUtility.TangentMode.Linear) ||
											(tanR == AnimationUtility.TangentMode.Constant))
										{
											tanR = AnimationUtility.TangentMode.Free;
										}
										if (ki < curveData.keyData.Count - 1)
										{
											key.outWeight = 1 / 3.0f;
										}
									}

									if (updateKeyFrame)
									{
										curveData.curve.MoveKey(ki, key);
									}
									AnimationUtility.SetKeyBroken(curveData.curve, ki, broken);

									var tanLOld = AnimationUtility.GetKeyLeftTangentMode(curveData.curve, ki);
									if (tanLOld != tanL)
									{
										AnimationUtility.SetKeyLeftTangentMode(curveData.curve, ki, tanL);
									}

									var tanROld = AnimationUtility.GetKeyRightTangentMode(curveData.curve, ki);
									if (tanROld != tanR)
									{
										AnimationUtility.SetKeyRightTangentMode(curveData.curve, ki, tanR);
									}
								}

								EditorUtility.DisplayProgressBar("Converting animation clip tangent.", $"{curveData.path}:{curveData.propertyName}", (float)(progress + 1) / totalProgress);
								AnimationUtility.SetEditorCurve(clipData.animationClip, curveData.binding, curveData.curve);

								modified = true;
								totalProgress++;
							}
						}
						progress++;
					}
				}
				if (!modified)
				{
					EditorUtility.SetDirty(clipData.animationClip);
				}
			}
			AssetDatabase.SaveAssets();

			EditorUtility.ClearProgressBar();
		}

		public static float CalculateSmoothTangent(Keyframe key)
		{
			if (key.inTangent == float.PositiveInfinity)
			{
				key.inTangent = 0f;
			}

			if (key.outTangent == float.PositiveInfinity)
			{
				key.outTangent = 0f;
			}
			return (key.outTangent + key.inTangent) * 0.5f;
		}

		void DetectCurveMode(AnimationCurve curve,
			int keyIndex,
			bool broken,
			AnimationUtility.TangentMode tanL,
			AnimationUtility.TangentMode tanR,
			out int mainModeOut,
			out int subModeLeftOut,
			out int subModeRightOut,
			out int weightedLeftOut,
			out int weightedRightOut)
		{
			var keyFrame = curve.keys[keyIndex];

			if (!broken)
			{
				if ((tanL == AnimationUtility.TangentMode.ClampedAuto) || (tanR == AnimationUtility.TangentMode.ClampedAuto))
				{
					mainModeOut = (int)CurveMainMode.ClampedAuto;
				}
				else if ((tanL == AnimationUtility.TangentMode.Auto) || (tanR == AnimationUtility.TangentMode.Auto))
				{
					mainModeOut = (int)CurveMainMode.Auto;
				}
				else //if ((tanL == AnimationUtility.TangentMode.Free) || (tanR == AnimationUtility.TangentMode.Free))
				{
					mainModeOut = (int)CurveMainMode.FreeSmooth;

					if (keyFrame.inTangent == 0f && keyFrame.outTangent == 0f)
					{
						mainModeOut = (int)CurveMainMode.Flat;
					}
				}
				subModeLeftOut = subModeRightOut = (int)CurveSubMode.Free;
			}
			else
			{
				mainModeOut = (int)CurveMainMode.Broken;

				if (tanL == AnimationUtility.TangentMode.Free)
				{
					subModeLeftOut = (int)CurveSubMode.Free;
				}
				else if (tanL == AnimationUtility.TangentMode.Linear)
				{
					subModeLeftOut = (int)CurveSubMode.Linear;
				}
				else if (tanL == AnimationUtility.TangentMode.Constant)
				{
					subModeLeftOut = (int)CurveSubMode.Constant;
				}
				else
				{
					subModeLeftOut = (int)CurveSubMode.Free;
				}

				if (tanR == AnimationUtility.TangentMode.Free)
				{
					subModeRightOut = (int)CurveSubMode.Free;
				}
				else if (tanR == AnimationUtility.TangentMode.Linear)
				{
					subModeRightOut = (int)CurveSubMode.Linear;
				}
				else if (tanR == AnimationUtility.TangentMode.Constant)
				{
					subModeRightOut = (int)CurveSubMode.Constant;
				}
				else
				{
					subModeRightOut = (int)CurveSubMode.Free;
				}
			}

			weightedLeftOut = ((keyFrame.weightedMode & WeightedMode.In) != WeightedMode.None) ? 1 : 0;
			weightedRightOut = ((keyFrame.weightedMode & WeightedMode.Out) != WeightedMode.None) ? 1 : 0;
		}
	}
}
