import { DEVELOPMENT_ACTOR_ID } from "../../security/development-identity";

/** History has no person-name lookup. Never attribute another actor's decision to the current user. */
export function DecisionActor({ actorId }: { actorId?: string }) {
  if (!actorId) return null;

  if (actorId === DEVELOPMENT_ACTOR_ID) {
    return <span title={`User ID: ${actorId}`}>Recorded by <bdi>Development user</bdi> (test identity)</span>;
  }

  return <span>Recorded by user ID <bdi>{actorId}</bdi></span>;
}
