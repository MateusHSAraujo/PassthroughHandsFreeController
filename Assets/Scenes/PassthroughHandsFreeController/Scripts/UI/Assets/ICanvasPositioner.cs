using UnityEngine;

public interface ICanvasPositioner
{
    Transform targetCamera { get; }

    // User standard position method implemented by component
    void Position();

    // Position in a giver 3d position
    void Position(Vector3 position);

    // Position in the direction of a given object at a given distance
    void PositionAtDirectionOf(Vector3 referenceObjectPosition, float distance);

    // Position in the direction of a given object using the standard distance
    void PositionAtDirectionOf(Vector3 referenceObjectPosition);
}