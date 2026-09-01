using System.Collections.Generic;

/// <summary>
/// 세션 결과 데이터 컨테이너
/// MonoBehaviour 아님 — 순수 C# 데이터 클래스
/// SessionManager가 생성/관리, SettlementUI가 표시
/// </summary>
[System.Serializable]
public class SessionResultData
{
    // ─── 탈출 결과 ───────────────────────────────
    public bool extractionSuccess;

    // ─── 전투 데이터 ──────────────────────────────
    public int killCount;

    // ─── 경제 데이터 ──────────────────────────────
    public int solariumEarned;

    // ─── 탐색 데이터 ──────────────────────────────
    public float sessionDurationSeconds;

    // ─── 루팅 목록 ───────────────────────────────
    public List<string> lootedItemNames = new List<string>();

    // ─────────────────────────────────────────────
    //  세션 중 데이터 누적
    // ─────────────────────────────────────────────

    /// <summary>적 처치 등록 + 솔라늄 보상</summary>
    public void RegisterKill(int solariumPerKill = 10)
    {
        killCount++;
        solariumEarned += solariumPerKill;
    }

    /// <summary>아이템 루팅 등록 + 솔라늄 가치</summary>
    public void AddLoot(string itemName, int solariumValue = 0)
    {
        lootedItemNames.Add(itemName);
        solariumEarned += solariumValue;
    }

    /// <summary>탐색 시간 포맷 (mm:ss)</summary>
    public string GetFormattedDuration()
    {
        int min = (int)(sessionDurationSeconds / 60);
        int sec = (int)(sessionDurationSeconds % 60);
        return $"{min:00}:{sec:00}";
    }
}
