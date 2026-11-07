using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Identifies an icon of the armrest buttons. To add a new icon: add a value here (append only) and add one entry to
/// the shared <see cref="ArmrestIconSet"/> asset. The buttons and their prefab do not change.
/// </summary>
public enum ArmrestIcon { Menu, Minimize, Open }

/// <summary>
/// One shared asset that maps every <see cref="ArmrestIcon"/> to a sprite, so all armrest buttons use the same icon
/// language and the sprites are edited in a single place.
/// </summary>
[CreateAssetMenu(fileName = "ArmrestIconSet", menuName = "PassthroughHandsFreeController/Armrest Icon Set")]
public class ArmrestIconSet : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        public ArmrestIcon Id;
        public Sprite Sprite;
    }

    [SerializeField] private List<Entry> _entries = new();

    /// <summary>Returns the sprite for <paramref name="id"/>, or null when it has no entry.</summary>
    public Sprite Get(ArmrestIcon id)
    {
        foreach (Entry entry in _entries)
        {
            if (entry.Id == id) return entry.Sprite;
        }
        return null;
    }
}
