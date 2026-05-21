using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

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
    [SerializeField] private int maxMoves = 20;       
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TextMeshProUGUI movesText;
    [SerializeField] private GameObject gameOverPanel;

    [Header("--- Audio Settings (타격감) ---")]
    [SerializeField] private AudioSource audioSource; // 2번에서 추가할 오디오 소스 컴포넌트
    [SerializeField] private AudioClip matchSound;    // 외부에서 바꿀 수 있는 효과음 파일
    [SerializeField] private float pitchIncrease = 0.1f; // 콤보마다 증가할 음정 높이

    private BlockData[,] grid;
    private BlockData firstSelectedBlock; 
    private bool isSwapping = false;
    
    private int currentMoves;
    private int currentScore = 0;
    private bool isGameOver = false;

    // 현재 연쇄 폭발 횟수를 기억하는 변수
    private int comboCount = 1;

    private void Awake()
    {
        Instance = this;
        InitializeGame();
        InitializeBoard();
        SpawnBoardObjects();
    }

    private void InitializeGame()
    {
        currentMoves = maxMoves;
        currentScore = 0;
        isGameOver = false;
        comboCount = 1;
        
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
            currentMoves--;
            UpdateUI();
            
            // 첫 매칭이 성공했으므로 콤보 카운트를 1로 시작합니다.
            comboCount = 1;
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
        currentScore += matchedBlocks.Count * 100;
        UpdateUI();

        // 효과음 재생 함수 호출
        PlayMatchSound();

        foreach (BlockData block in matchedBlocks)
        {
            if (block.gameObject != null) Destroy(block.gameObject);
            block.type = BlockType.Empty;
        }
        StartCoroutine(ProcessBoardRoutine());
    }

    // 효과음을 재생하는 핵심 함수
    private void PlayMatchSound()
    {
        if (audioSource != null && matchSound != null)
        {
            // 콤보 횟수에 따라 음정(Pitch)을 계산합니다. (기본값 1.0부터 시작하여 콤보당 증가)
            // 연쇄적으로 터질 때 도-레-미-파 처럼 소리가 높아져 리듬감과 타격감이 생깁니다.
            audioSource.pitch = 1.0f + (comboCount - 1) * pitchIncrease;
            
            // 효과음을 1회 자르고 재생합니다.
            audioSource.PlayOneShot(matchSound);
        }
    }

    private IEnumerator ProcessBoardRoutine()
    {
        yield return StartCoroutine(DropBlocksRoutine());
        yield return StartCoroutine(RefillBoardRoutine());

        HashSet<BlockData> newMatches = FindAllMatches();
        if (newMatches.Count > 0)
        {
            // 연쇄 폭발이 일어났으므로 콤보 카운트 증가
            comboCount++;
            
            yield return new WaitForSeconds(0.3f); 
            ClearMatches(newMatches); 
        }
        else
        {
            // 더 이상 터질 게 없다면 다음 턴을 위해 콤보 카운트 리셋
            comboCount = 1;
            
            CheckGameOver();
            if (!isGameOver) isSwapping = false; 
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

    public void RestartGame()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}