using UnityEngine;

#nullable enable

namespace Uchuhikoshi.AnimationClipTangentConverter
{
	[CreateAssetMenu(fileName = "new_AnimationClipTangentConverter", menuName = "Uchuhikoushi/Create AnimationClipTangentConverter Settings")]
	public sealed class AnimationClipTangentConverter : ScriptableObject
	{
		public AnimationClip?[] targetAnimationClips = new AnimationClip?[0];
	}
}
