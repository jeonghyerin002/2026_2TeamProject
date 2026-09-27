using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "DialogDatabaseSO", menuName = "Dialog System/DialogDatabaseSO")]
public class DialogDatabaseSO : ScriptableObject
{
    public List<DialogSO> dialogs = new List<DialogSO>();

    Dictionary<int, DialogSO> dialogById;

    public void Initialize()
    {
        dialogById = new Dictionary<int, DialogSO>();
        foreach (var dialog in dialogs)
        {
            if (dialog != null && !dialogById.ContainsKey(dialog.id))
            {
                dialogById.Add(dialog.id, dialog);
            }
        }
    }

    public DialogSO GetDialogById(int id)
    {
        if (dialogById == null)
        {
            Initialize();
        }

        if (dialogById.TryGetValue(id, out var dialog))
        {
            return dialog;
        }

        return null;
    }
}
