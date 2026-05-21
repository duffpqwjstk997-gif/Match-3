using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

public class BoardGridManager : MonoBehaviour
{
    public static BoardGridManager Instance;

    public enum BlockType { Empty = 0, Music, Phone, KakaoTalk, Book, Podcast, AppStore, Camera, Photos, CrossItem }

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

    [Header("--- Audio Settings ---")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip[] blockMatchSounds; 
    [SerializeField] private float pitchIncrease = 0.1f; 

    private BlockData[,] grid;
    private BlockData firstSelectedBlock; 
    private bool isSwapping = false;
    
    private int currentMoves;
    private int currentScore = 0;
    private bool isGameOver = false;
    private int comboCount = 1;

    private List<Vector2Int> itemSpawnPositions = new List<Vector2Int>();

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
        itemSpawnPositions.Clear();
        
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
                for (int i = 1; i <= 8; i++) { possibleTypes.Add((BlockType)i); }

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

        if (a.type == BlockType.CrossItem || b.type == BlockType.CrossItem)
        {
            currentMoves--;
            UpdateUI();
            
            BlockData itemBlock = (a.type == BlockType.CrossItem) ? a : b;
            TriggerCrossClear(itemBlock.x, itemBlock.y);
            yield break; 
        }

        HashSet<BlockData> matchedBlocks = FindAllMatches();
        if (matchedBlocks.Count > 0)
        {
            currentMoves--;
            UpdateUI();
            
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

    // ★수정됨★: 5개 매치에서 4개 매치로 기준이 낮아졌습니다.
    private HashSet<BlockData> FindAllMatches()
    {
        HashSet<BlockData> matches = new HashSet<BlockData>();
        itemSpawnPositions.Clear();

        // 1. 가로 4개 매치 우선 탐색 (x 범위가 width - 3으로 변경됨)
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width - 3; x++)
            {
                BlockType type = grid[x, y].type;
                if (type == BlockType.Empty || type == BlockType.CrossItem) continue;

                // 4개가 연속으로 같은지 검사
                if (grid[x+1, y].type == type && grid[x+2, y].type == type && grid[x+3, y].type == type)
                {
                    matches.Add(grid[x, y]); matches.Add(grid[x+1, y]); matches.Add(grid[x+2, y]); matches.Add(grid[x+3, y]);
                    // 4개 중 2번째([x+1, y]) 좌표를 아이템 탄생 위치로 지정
                    itemSpawnPositions.Add(new Vector2Int(x + 1, y)); 
                }
            }
        }

        // 2. 세로 4개 매치 우선 탐색 (y 범위가 height - 3으로 변경됨)
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height - 3; y++)
            {
                BlockType type = grid[x, y].type;
                if (type == BlockType.Empty || type == BlockType.CrossItem) continue;

                // 4개가 연속으로 같은지 검사
                if (grid[x, y+1].type == type && grid[x, y+2].type == type && grid[x, y+3].type == type)
                {
                    matches.Add(grid[x, y]); matches.Add(grid[x, y+1]); matches.Add(grid[x, y+2]); matches.Add(grid[x, y+3]);
                    // 4개 중 2번째([x, y+1]) 좌표를 아이템 탄생 위치로 지정
                    itemSpawnPositions.Add(new Vector2Int(x, y + 1));
                }
            }
        }

        // 3. 기존의 일반 3개 매치 탐색
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width - 2; x++)
            {
                BlockData current = grid[x, y];
                if (current.type == BlockType.Empty || current.type == BlockType.CrossItem) continue;
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
                if (current.type == BlockType.Empty || current.type == BlockType.CrossItem) continue;
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

        PlayMatchSounds(matchedBlocks);

        List<Vector2Int> spawnTargets = new List<Vector2Int>(itemSpawnPositions);
        itemSpawnPositions.Clear(); 

        foreach (BlockData block in matchedBlocks)
        {
            if (block.gameObject != null) Destroy(block.gameObject);

            Vector2Int currentPos = new Vector2Int(block.x, block.y);
            
            if (spawnTargets.Contains(currentPos))
            {
                block.type = BlockType.CrossItem;
                
                GameObject newItem = Instantiate(blockPrefab, GetWorldPosition(block.x, block.y), Quaternion.identity, this.transform);
                newItem.name = $"Item_Cross_[{block.x}, {block.y}]";
                
                BlockItem blockItem = newItem.GetComponent<BlockItem>();
                blockItem.Setup(BlockType.CrossItem, blockSprites[(int)BlockType.CrossItem - 1], block.x, block.y);
                
                block.gameObject = newItem;
            }
            else
            {
                block.type = BlockType.Empty;
            }
        }
        StartCoroutine(ProcessBoardRoutine());
    }

    private void TriggerCrossClear(int targetX, int targetY)
    {
        HashSet<BlockData> blocksToClear = new HashSet<BlockData>();

        for (int x = 0; x < width; x++)
        {
            if (grid[x, targetY].type != BlockType.Empty)
                blocksToClear.Add(grid[x, targetY]);
        }

        for (int y = 0; y < height; y++)
        {
            if (grid[targetX, y].type != BlockType.Empty)
                blocksToClear.Add(grid[targetX, y]);
        }
        
        comboCount = 2; 
        ClearMatches(blocksToClear);
    }

    private void PlayMatchSounds(HashSet<BlockData> matchedBlocks)
    {
        if (audioSource == null || blockMatchSounds == null || blockMatchSounds.Length == 0) return;

        HashSet<BlockType> uniqueTypesInMatch = new HashSet<BlockType>();
        foreach (BlockData block in matchedBlocks)
        {
            if (block.type != BlockType.Empty) uniqueTypesInMatch.Add(block.type);
        }

        foreach (BlockType type in uniqueTypesInMatch)
        {
            int index = (int)type - 1;
            if (index >= 0 && index < blockMatchSounds.Length && blockMatchSounds[index] != null)
            {
                audioSource.pitch = 1.0f + (comboCount - 1) * pitchIncrease;
                audioSource.PlayOneShot(blockMatchSounds[index]);
            }
        }
    }

    private IEnumerator ProcessBoardRoutine()
    {
        yield return StartCoroutine(DropBlocksRoutine());
        yield return StartCoroutine(RefillBoardRoutine());

        HashSet<BlockData> newMatches = FindAllMatches();
        if (newMatches.Count > 0)
        {
            comboCount++;
            yield return new WaitForSeconds(0.3f); 
            ClearMatches(newMatches); 
        }
        else
        {
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
                    BlockType randomType = (BlockType)Random.Range(1, 9); 
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