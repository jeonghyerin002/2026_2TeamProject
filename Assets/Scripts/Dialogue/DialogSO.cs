using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "DialogSO", menuName = "Dialog System/DialogSO")]
public class DialogSO : ScriptableObject
{
    [Header("Dialog Info")]
    public int id;
    public string characterName;

    [TextArea(3, 6)]
    public string text;
    public int nextId;

    [Header("Optional Setting")]
    public Sprite portrait; //추후 초상화 사용 (포켓몬스터 처럼)
    public List<DialogChoiceSO> choices = new List<DialogChoiceSO>();
}
