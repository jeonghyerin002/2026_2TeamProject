using UnityEngine;
using UnityEngine.UI;

/// <summary>프롤로그 건너뛰기 버튼의 아래 방향 삼각형을 표시한다.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class PrologueSkipGraphic : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        var rect = GetPixelAdjustedRect();
        mesh.AddVert(new Vector3(rect.xMin, rect.yMax), color, Vector2.zero);
        mesh.AddVert(new Vector3(rect.xMax, rect.yMax), color, Vector2.zero);
        mesh.AddVert(new Vector3(rect.center.x, rect.yMin), color, Vector2.zero);
        mesh.AddTriangle(0, 1, 2);
    }
}
