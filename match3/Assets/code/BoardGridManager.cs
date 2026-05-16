using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BoardGridManager : MonoBehaviour
{
    // 누구나 쉽게 이 매니저에 접근할 수 있게 만드는 싱글톤
    public static BoardGridManager Instance;

    public enum BlockType { Empty = 0, Music, Phone, KakaoTalk, Book, Podcast, AppStore, Camera, Photos }

    [System.Serializable]
    public class BlockData
    {
        public int x; public int y;
        public BlockType type;
        public GameObject gameObject;

        public BlockData(int x, int y, BlockType type) { this.x = x; this.y = y; this.type = type; }
    }

    [Header("--- Grid Settings ---")]
    [SerializeField] private int width = 7;
    [SerializeField] private int height = 7;
    [SerializeField] private float cellSize = 1.1f;

    [Header("--- Visual Elements ---")]
    [SerializeField] private GameObject blockPrefab;
    [SerializeField] private Sprite[] blockSprites;

    private BlockData[,] grid;

    // --- 스왑(교체) 관련 변수 ---
    private BlockData firstSelectedBlock; // 처음 클릭한 블록
    private bool isSwapping = false;      // 현재 블록이 이동 중인지 체크 (이동 중엔 터치 방지)

    private void Awake()
    {
        Instance = this;
        InitializeBoard();
        SpawnBoardObjects();
    }

    private void InitializeBoard()
    {
        grid = new BlockData[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                List<BlockType> possibleTypes = new List<BlockType>();
                for (int i = 1; i <= System.Enum.GetValues(typeof(BlockType)).Length - 1; i++) { possibleTypes.Add((BlockType)i); }

                if (x >= 2 && grid[x - 1, y].type == grid[x - 2, y].type) { possibleTypes.Remove(grid[x - 1, y].type); }
                if (y >= 2 && grid[x, y - 1].type == grid[x, y - 2].type) { possibleTypes.Remove(grid[x, y - 1].type); }

                BlockType chosenType = possibleTypes[Random.Range(0, possibleTypes.Count)];
                grid[x, y] = new BlockData(x, y, chosenType);
            }
        }
    }

    private void SpawnBoardObjects()
    {
        Vector3 startPosition = new Vector3(-(width - 1) * cellSize / 2f, -(height - 1) * cellSize / 2f, 0);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                BlockData data = grid[x, y];
                Vector3 spawnPos = startPosition + new Vector3(x * cellSize, y * cellSize, 0);

                GameObject newBlock = Instantiate(blockPrefab, spawnPos, Quaternion.identity, this.transform);
                newBlock.name = $"Block_ [{x}, {y}]";

                Sprite targetSprite = blockSprites[(int)data.type - 1];

                BlockItem blockItem = newBlock.GetComponent<BlockItem>();
                if (blockItem != null)
                {
                    // BlockItem 쪽에 자신의 X, Y 좌표도 함께 넘겨줍니다.
                    blockItem.Setup(data.type, targetSprite, x, y);
                }

                data.gameObject = newBlock;
            }
        }
    }

    // --- 4단계 추가: 블록 선택 및 스왑 로직 ---

    public void SelectBlock(int x, int y)
    {
        // 이동 중일 때는 다른 블록을 클릭하지 못하게 차단
        if (isSwapping) return;

        BlockData clickedBlock = grid[x, y];

        // 1. 아무것도 선택되지 않은 상태라면 첫 번째 블록으로 지정
        if (firstSelectedBlock == null)
        {
            firstSelectedBlock = clickedBlock;
            // (선택 시 약간 커지게 하거나 투명도를 조절하는 시각적 효과를 넣으면 좋습니다)
        }
        else
        {
            // 2. 이미 첫 번째 블록이 선택된 상태라면 두 번째 클릭으로 간주
            // 같은 블록을 한 번 더 누르면 선택 취소
            if (firstSelectedBlock == clickedBlock)
            {
                firstSelectedBlock = null;
                return;
            }

            // 인접한(상하좌우 1칸 차이) 블록인지 검사
            if (IsAdjacent(firstSelectedBlock, clickedBlock))
            {
                // 조건을 만족하면 자리를 바꾸는 코루틴 실행
                StartCoroutine(SwapRoutine(firstSelectedBlock, clickedBlock));
            }

            // 검사가 끝났으므로 선택 상태 초기화
            firstSelectedBlock = null;
        }
    }

    // 수학적으로 가로 거리 + 세로 거리가 1이면 상하좌우로 붙어있는 것
    private bool IsAdjacent(BlockData a, BlockData b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y) == 1;
    }

    // 두 블록의 시각적 위치와 내부 데이터 위치를 교환하는 코루틴
    private IEnumerator SwapRoutine(BlockData a, BlockData b)
    {
        isSwapping = true;

        Transform transformA = a.gameObject.transform;
        Transform transformB = b.gameObject.transform;

        Vector3 posA = transformA.position;
        Vector3 posB = transformB.position;

        float duration = 0.2f; // 교체되는 데 걸리는 시간(초)
        float elapsed = 0f;

        // 부드럽게 위치 이동 (Lerp 사용)
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            transformA.position = Vector3.Lerp(posA, posB, t);
            transformB.position = Vector3.Lerp(posB, posA, t);
            yield return null; // 다음 프레임까지 대기
        }

        // 최종 위치 확실하게 고정
        transformA.position = posB;
        transformB.position = posA;

        // --- 내부 2차원 배열 데이터 갱신 ---
        grid[a.x, a.y] = b;
        grid[b.x, b.y] = a;

        // 임시 변수를 사용해 a와 b의 논리적 좌표값 교체
        int tempX = a.x; int tempY = a.y;
        a.x = b.x; a.y = b.y;
        b.x = tempX; b.y = tempY;

        // BlockItem 스크립트가 가지고 있는 좌표값도 갱신
        a.gameObject.GetComponent<BlockItem>().UpdateCoordinates(a.x, a.y);
        b.gameObject.GetComponent<BlockItem>().UpdateCoordinates(b.x, b.y);

        isSwapping = false;

        // TODO: 5단계에서 여기에 '매치가 성립되었는지 검사하는 로직'이 들어갈 예정입니다.
    }
}