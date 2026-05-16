using UnityEngine;

public class BlockItem : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    public BoardGridManager.BlockType blockType;

    // 자신이 현재 배열의 몇 콤마 몇(x, y)에 있는지 기억합니다.
    public int x;
    public int y;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    public void Setup(BoardGridManager.BlockType type, Sprite sprite, int startX, int startY)
    {
        this.blockType = type;
        this.x = startX;
        this.y = startY;

        if (spriteRenderer != null)
        {
            spriteRenderer.sprite = sprite;
        }
    }

    // 자리가 바뀌었을 때 좌표를 갱신해주는 함수
    public void UpdateCoordinates(int newX, int newY)
    {
        x = newX;
        y = newY;
    }

    // 마우스나 터치로 이 오브젝트(BoxCollider2D)를 클릭했을 때 실행되는 유니티 내장 함수
    private void OnMouseDown()
    {
        // 콘솔창에 로그를 띄워서 클릭 감지 여부부터 확인
        Debug.Log($"[{x}, {y}] 블록이 클릭되었습니다!");
        BoardGridManager.Instance.SelectBlock(x, y);
    }
}