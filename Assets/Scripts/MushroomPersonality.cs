using UnityEngine;

public abstract class MushroomPersonality : MonoBehaviour
{
    protected MushroomAI mushroomAI;
    protected MushroomData data;

    public virtual void Initialize(MushroomAI ai, MushroomData mushroomData)
    {
        mushroomAI = ai;
        data = mushroomData;
    }

    public abstract void UpdateBehavior();

    // Allow AI-level ambient wandering while in Idle. Override to disable.
    public virtual bool AllowAmbientIdleWander => true;

    public virtual void OnStateChanged(MushroomState fromState, MushroomState toState)
    {
        // Override THIS!!!
    }

    protected void ChangeState(MushroomState newState)
    {
        if (mushroomAI != null)
            mushroomAI.ChangeState(newState);
    }

    public virtual void OnTongueAttached()
    {
        
    }

    public virtual void OnTongueReleased()
    {
        
    }
}

