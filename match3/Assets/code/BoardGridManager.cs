using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro; // UI 제어를 위해 추가
using UnityEngine.SceneManagement; // 씬 재시작을 위해 추가

public class BoardGridManager : MonoBehaviour
{
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

    [Header("--- Game Flow & UI ---")]
    [SerializeField] private int maxMoves = 20;       // 최대 이동 가능 횟수
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TextMeshProUGUI movesText;
    [SerializeField] private GameObject gameOverPanel;

    private BlockData[,] grid;
    private BlockData firstSelectedBlock;
    private bool isSwapping = false;

    // 게임 상태 변수
    private int currentMoves;
    private int currentScore = 0;
    private bool isGameOver = false;

    private void Awake()
    {
        Instance = this;
        InitializeGame();
        InitializeBoard();
        SpawnBoardObjects();
    }

    // 7단계 추가: 게임 초기 상태 세팅
    private void InitializeGame()
    {
        currentMoves = maxMoves;
        currentScore = 0;
        isGameOver = false;

        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (scoreText != null) scoreText.text = $"Score: {currentScore}";
        if (movesText != null) movesText.text = $"Moves: {currentMoves}";
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

                if (x >= 2 && grid[x - 1, y].type == grid[x - 2, y].type) possibleTypes.Remove(grid[x - 1, y].type);
                if (y >= 2 && grid[x, y - 1].type == grid[x, y - 2].type) possibleTypes.Remove(grid[x, y - 1].type);

                BlockType chosenType = possibleTypes[Random.Range(0, possibleTypes.Count)];
                grid[x, y] = new BlockData(x, y, chosenType);
            }
        }
    }

    private Vector3 GetWorldPosition(int x, int y)
    {
        Vector3 startPosition = new Vector3(-(width - 1) * cellSize / 2f, -(height - 1) * cellSize / 2f, 0);
        return startPosition + new Vector3(x * cellSize, y * cellSize, 0);
    }

    private void SpawnBoardObjects()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                BlockData data = grid[x, y];
                Vector3 spawnPos = GetWorldPosition(x, y);

                GameObject newBlock = Instantiate(blockPrefab, spawnPos, Quaternion.identity, this.transform);
                newBlock.name = $"Block_ [{x}, {y}]";

                BlockItem blockItem = newBlock.GetComponent<BlockItem>();
                if (blockItem != null) blockItem.Setup(data.type, blockSprites[(int)data.type - 1], x, y);

                data.gameObject = newBlock;
            }
        }
    }

    public void SelectBlock(int x, int y)
    {
        // 게임 오버 상태이거나 블록 이동 중이면 입력 차단
        if (isSwapping || isGameOver) return;

        BlockData clickedBlock = grid[x, y];

        if (firstSelectedBlock == null) firstSelectedBlock = clickedBlock;
        else
        {
            if (firstSelectedBlock == clickedBlock) { firstSelectedBlock = null; return; }
            if (IsAdjacent(firstSelectedBlock, clickedBlock)) StartCoroutine(SwapRoutine(firstSelectedBlock, clickedBlock));
            firstSelectedBlock = null;
        }
    }

    private bool IsAdjacent(BlockData a, BlockData b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y) == 1;
    }

    private IEnumerator SwapRoutine(BlockData a, BlockData b)
    {
        isSwapping = true;

        Vector3 posA = a.gameObject.transform.position;
        Vector3 posB = b.gameObject.transform.position;

        Coroutine moveA = StartCoroutine(AnimateMove(a.gameObject.transform, posB, 0.2f));
        Coroutine moveB = StartCoroutine(AnimateMove(b.gameObject.transform, posA, 0.2f));
        yield return moveA;
        yield return moveB;

        SwapLogicalData(a, b);

        HashSet<BlockData> matchedBlocks = FindAllMatches();
        if (matchedBlocks.Count > 0)
        {
            // 매칭 성공 시 이동 횟수 1 차감
            currentMoves--;
            UpdateUI();

            ClearMatches(matchedBlocks);
        }
        else
        {
            SwapLogicalData(a, b);

            moveA = StartCoroutine(AnimateMove(a.gameObject.transform, posA, 0.2f));
            moveB = StartCoroutine(AnimateMove(b.gameObject.transform, posB, 0.2f));
            yield return moveA;
            yield return moveB;

            isSwapping = false;
        }
    }

    private IEnumerator AnimateMove(Transform objTransform, Vector3 targetPos, float duration)
    {
        if (objTransform == null) yield break;
        Vector3 startPos = objTransform.position;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (objTransform == null) yield break;
            elapsed += Time.deltaTime;
            objTransform.position = Vector3.Lerp(startPos, targetPos, elapsed / duration);
            yield return null;
        }
        if (objTransform != null) objTransform.position = targetPos;
    }

    private void SwapLogicalData(BlockData a, BlockData b)
    {
        grid[a.x, a.y] = b; grid[b.x, b.y] = a;
        int tempX = a.x; int tempY = a.y;
        a.x = b.x; a.y = b.y;
        b.x = tempX; b.y = tempY;
        a.gameObject.GetComponent<BlockItem>().UpdateCoordinates(a.x, a.y);
        b.gameObject.GetComponent<BlockItem>().UpdateCoordinates(b.x, b.y);
    }

    private HashSet<BlockData> FindAllMatches()
    {
        HashSet<BlockData> matches = new HashSet<BlockData>();
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width - 2; x++)
            {
                BlockData current = grid[x, y];
                if (current.type == BlockType.Empty) continue;
                if (grid[x + 1, y].type == current.type && grid[x + 2, y].type == current.type)
                {
                    matches.Add(current); matches.Add(grid[x + 1, y]); matches.Add(grid[x + 2, y]);
                }
            }
        }
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height - 2; y++)
            {
                BlockData current = grid[x, y];
                if (current.type == BlockType.Empty) continue;
                if (grid[x, y + 1].type == current.type && grid[x, y + 2].type == current.type)
                {
                    matches.Add(current); matches.Add(grid[x, y + 1]); matches.Add(grid[x, y + 2]);
                }
            }
        }
        return matches;
    }

    private void ClearMatches(HashSet<BlockData> matchedBlocks)
    {
        // 파괴되는 블록 1개당 100점 추가
        currentScore += matchedBlocks.Count * 100;
        UpdateUI();

        foreach (BlockData block in matchedBlocks)
        {
            if (block.gameObject != null) Destroy(block.gameObject);
            block.type = BlockType.Empty;
        }
        StartCoroutine(ProcessBoardRoutine());
    }

    private IEnumerator ProcessBoardRoutine()
    {
        yield return StartCoroutine(DropBlocksRoutine());
        yield return StartCoroutine(RefillBoardRoutine());

        HashSet<BlockData> newMatches = FindAllMatches();
        if (newMatches.Count > 0)
        {
            yield return new WaitForSeconds(0.3f);
            ClearMatches(newMatches);
        }
        else
        {
            // 모든 연쇄 폭발과 보드 정리가 끝난 후 게임 오버를 체크합니다.
            CheckGameOver();

            if (!isGameOver)
                isSwapping = false;
        }
    }

    private void CheckGameOver()
    {
        if (currentMoves <= 0)
        {
            isGameOver = true;
            if (gameOverPanel != null) gameOverPanel.SetActive(true);
            Debug.Log($"게임 종료! 최종 점수: {currentScore}");
        }
    }

    private IEnumerator DropBlocksRoutine()
    {
        bool isAnyBlockDropped = false;
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (grid[x, y].type == BlockType.Empty)
                {
                    for (int ny = y + 1; ny < height; ny++)
                    {
                        if (grid[x, ny].type != BlockType.Empty)
                        {
                            BlockData dropBlock = grid[x, ny];
                            grid[x, y] = dropBlock;
                            grid[x, ny] = new BlockData(x, ny, BlockType.Empty);

                            dropBlock.x = x; dropBlock.y = y;
                            dropBlock.gameObject.GetComponent<BlockItem>().UpdateCoordinates(x, y);

                            StartCoroutine(AnimateMove(dropBlock.gameObject.transform, GetWorldPosition(x, y), 0.2f));
                            isAnyBlockDropped = true;
                            break;
                        }
                    }
                }
            }
        }
        if (isAnyBlockDropped) yield return new WaitForSeconds(0.2f);
    }

    private IEnumerator RefillBoardRoutine()
    {
        bool isAnyBlockRefilled = false;
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (grid[x, y].type == BlockType.Empty)
                {
                    BlockType randomType = (BlockType)Random.Range(1, System.Enum.GetValues(typeof(BlockType)).Length);
                    grid[x, y].type = randomType;

                    Vector3 dropTargetPos = GetWorldPosition(x, y);
                    Vector3 spawnPos = dropTargetPos + new Vector3(0, height * cellSize, 0);

                    GameObject newBlock = Instantiate(blockPrefab, spawnPos, Quaternion.identity, this.transform);
                    newBlock.name = $"Block_ [{x}, {y}]";

                    BlockItem blockItem = newBlock.GetComponent<BlockItem>();
                    blockItem.Setup(randomType, blockSprites[(int)randomType - 1], x, y);

                    grid[x, y].gameObject = newBlock;

                    StartCoroutine(AnimateMove(newBlock.transform, dropTargetPos, 0.25f));
                    isAnyBlockRefilled = true;
                }
            }
        }
        if (isAnyBlockRefilled) yield return new WaitForSeconds(0.25f);
    }

    // 버튼 이벤트 연결용 함수: 게임 재시작
    public void RestartGame()
    {
        // 현재 열려있는 씬을 다시 로드합니다.
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}