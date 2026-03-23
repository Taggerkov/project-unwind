namespace FixedLogic
{
    public interface ITickable
    {
        void InputTick() { } // Optional: process input for the current tick
        void LogicTick() { } // Optional: update game logic for the current tick (e.g., physics, state changes)
        void UITick() { } // Optional: update UI elements for the current tick
    }

    public interface IInterpolatable {
        void Interpolate(float alpha); // Visuals: moving the 3D model
    }
}