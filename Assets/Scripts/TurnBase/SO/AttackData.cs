using UnityEngine;

[CreateAssetMenu(fileName = "AttackData", menuName = "TurnBased/AttackData")]
public class AttackData : ScriptableObject
{
    public string attackName;
    public TypeData typeData;
    public int fullCount;
    public int currentCount;

    public void ResetCount()
    {
        currentCount = fullCount;
    }
}
