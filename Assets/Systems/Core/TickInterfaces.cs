namespace Systems.Core
{
    /// <summary>
    /// Interface for objects that need to be updated every tick. The generic represent the class responsable for ticking the object that implements this interface.
    /// </summary>
    public interface ITickable<T>
    {
        void InputTick() { } // Optional: process input for the current tick
        void LogicTick() { } // Optional: update game logic for the current tick (e.g., physics, state changes)
        void UITick() { } // Optional: update UI elements for the current tick
    }

    /// <summary>
    /// Ignore, not implemented yet.
    /// </summary>
    public interface IInterpolatable {
        void Interpolate(float alpha); // Visuals: moving the 3D model
    }
}