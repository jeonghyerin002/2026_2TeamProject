using UnityEngine;

[CreateAssetMenu(fileName = "DialogChoiceSO", menuName = "Dialog System/DialogChoiceSO")]
public class DialogChoiceSO : ScriptableObject
{
    public string text;
    public int nextId; 
}
