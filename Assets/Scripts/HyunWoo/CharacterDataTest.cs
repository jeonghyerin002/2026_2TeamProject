using Game.Data;
using UnityEngine;
namespace Game.Data
{
    /// <summary>CharacterData SO 값을 Console에 출력해 확인한다.</summary>
    public class CharacterDataTest : MonoBehaviour
    {
        [SerializeField] private CharacterData characterData;

        // 연결된 CharacterData의 값을 출력
        private void Start()
        {
            Debug.Log($"ID: {characterData.Id}");
            Debug.Log($"Name: {characterData.Name}");
            Debug.Log($"MaxHP: {characterData.MaxHp}");
            Debug.Log($"Attack: {characterData.Attack}");
            Debug.Log($"Defense: {characterData.Defense}");
            Debug.Log($"SpecialAttack: {characterData.SpecialAttack}");
            Debug.Log($"SpecialDefense: {characterData.SpecialDefense}");
            Debug.Log($"Speed: {characterData.Speed}");
        }
    }
}