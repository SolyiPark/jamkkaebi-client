namespace MobilePrototype
{
    // Conditional gestures leave ordinary swipes to navigation until they explicitly capture input.
    public interface IMirrorGestureOwner
    {
        bool OwnsGesture { get; }
    }
}
