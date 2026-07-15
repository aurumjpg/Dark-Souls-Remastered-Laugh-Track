using DSLaughTrack.Logging;

namespace DSLaughTrack.Triggers;

public static class TriggerFactory
{
    public static IReadOnlyList<ITrigger> Build(AnimationIds ids, Log log)
    {
        var list = new List<ITrigger>
        {
            new OutOfStaminaTrigger(),
            new TookDamageTrigger(),
            new DeathTrigger(),
            new DexIncreaseTrigger(),
        };

        AddAnim(list, ids, log, "runningJump");
        AddAnim(list, ids, log, "gotParried");

        if (ids.Get("emptyEstus") is int estusAnim)
            list.Add(new AnimationTrigger("emptyEstus", estusAnim, s => s.EstusCount == 0));
        else
            LogDisabled(log, "emptyEstus");

        if (ids.Get("hitWall") is int wallAnim)
            list.Add(new AnimationTrigger("hitWall", wallAnim));
        else
            LogDisabled(log, "hitWall");

        if (ids.Get("parryAttempt") is int parryAnim)
            list.Add(new FailedParryTrigger(parryAnim, ids.Get("parrySuccess"), TimeSpan.FromMilliseconds(800)));
        else
            LogDisabled(log, "failedParry");

        return list;
    }

    private static void AddAnim(List<ITrigger> list, AnimationIds ids, Log log, string key)
    {
        if (ids.Get(key) is int anim) list.Add(new AnimationTrigger(key, anim));
        else LogDisabled(log, key);
    }

    private static void LogDisabled(Log log, string key) =>
        log.Warn($"{key}: no verified animation ID in animation_ids.json — trigger disabled");
}
