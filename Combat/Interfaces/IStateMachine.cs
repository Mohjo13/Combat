#region Using Directives

// No Unity-specific using needed here because this is a plain C# interface file.

#endregion

/// <summary>
/// Contract for anything that uses a state machine.
/// 
/// In this combat system, the Agent uses this to move between states
/// like Idle, Moving, Attacking, Blocking, Parrying, Staggered, and Dead.
/// 
/// Keeping this as an interface helps other systems interact with state
/// logic without needing to know the exact class type.
/// </summary>
public interface IStateMachine
{
    #region Properties

    /// <summary>
    /// The current active state of the object.
    /// </summary>
    AgentState CurrentState { get; }

    #endregion

    #region Methods

    /// <summary>
    /// Changes the object from its current state to a new one.
    /// 
    /// The implementing class decides what extra logic should happen
    /// during the transition, such as validation, animation triggers,
    /// or cleanup of the previous state.
    /// </summary>
    /// <param name="newState">The state to switch to.</param>
    void ChangeState(AgentState newState);

    #endregion
}