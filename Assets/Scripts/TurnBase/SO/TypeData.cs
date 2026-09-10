using UnityEngine;

[CreateAssetMenu(fileName = "TypeData", menuName = "TurnBased/TypeData")]
public class TypeData : ScriptableObject
{
    public string typeName;
    public float damageMultiplier = 1.0f; //상성에 따른 공격력(예비)
}
