using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName ="CardData",menuName ="Cards/CardData")]
public class CardData : ScriptableObject
{
    public string cardName;
    public string descrition;
    public int useCosts;
    public Texture2D image;
}
