// Suggestions describe the reviewer's action; they never supply source evidence.
export const additionReasons = ["Shown on the document but missed."] as const;

export const changeOrClearReasons = [
  "Read incorrectly by the system.",
  "This value is not on the document.",
  "I cannot verify this value.",
] as const;

export const clearAddedReasons = [
  "Added by mistake.",
  "I cannot verify this value.",
] as const;
