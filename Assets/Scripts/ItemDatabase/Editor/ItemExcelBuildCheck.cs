using System;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace TeamProject.ItemManagement.Editor
{
    public sealed class ItemExcelBuildCheck : IPreprocessBuildWithReport
    {
        public int callbackOrder => -100;
        public void OnPreprocessBuild(BuildReport report)
        {
            try { ItemExcelAutoConverter.Convert(); }
            catch (Exception exception)
            {
                throw new BuildFailedException("아이템 엑셀을 변환할 수 없습니다. 수정 후 다시 빌드하세요. " + exception.Message);
            }
        }
    }
}
