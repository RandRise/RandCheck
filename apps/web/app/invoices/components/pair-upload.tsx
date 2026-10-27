import { useEffect, useRef, useState } from "react";
import { clearProcessingIntent, needsSeparateProcessingIntent, prepareUploadIntent,
  processingIntentStorageKey, uploadFingerprint, uploadToQueue, type ProcessingIntent } from "../../document-processing";
import { useEnterprise } from "../../security/enterprise-provider";
import type { Document, Role } from "../workflow.types";
import { labels } from "../workflow.constants";

/** Upload receipt and selection are distinct writes: an accepted file is never uploaded again to retry a selection. */
export function PairUpload({ role, disabled, onUploaded, onBusy }: {
  role: Role; disabled: boolean; onUploaded: (document: Document) => Promise<void>; onBusy: (busy: boolean) => void;
}) {
  const { session } = useEnterprise();
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState("");
  const [separate, setSeparate] = useState(false);
  const [retryFile, setRetryFile] = useState<File | null>(null);
  const intent = useRef<ProcessingIntent | null>(null);
  const active = useRef(true);
  const locked = useRef(false);
  const storageKey = `${processingIntentStorageKey("upload", session.organizationId, session.actorId)}:pair:${role}`;

  useEffect(() => { active.current = true; return () => { active.current = false; }; }, []);

  async function upload(file: File, startSeparate = false) {
    if (disabled || locked.current || separate && !startSeparate) return;
    if (startSeparate) {
      clearProcessingIntent(storageKey, intent.current?.requestKey);
      intent.current = null;
    }
    locked.current = true;
    setBusy(true); onBusy(true); setMessage(""); setSeparate(false); setRetryFile(null);
    let accepted = false;
    try {
      const { fingerprint, durable } = await uploadFingerprint(file);
      if (!active.current) return;
      intent.current = prepareUploadIntent(intent.current, file, fingerprint, durable, storageKey);
      const result = await uploadToQueue<Document>(file, intent.current.requestKey);
      accepted = true;
      clearProcessingIntent(storageKey, intent.current.requestKey);
      intent.current = null;
      if (!active.current) return;
      await onUploaded(result);
      setMessage("File uploaded. Its values still need checking.");
    } catch (error) {
      if (!active.current) return;
      setSeparate(needsSeparateProcessingIntent(error));
      // Keep the same File object for retries when browser hashing/storage is unavailable.
      setRetryFile(accepted ? null : file);
      setMessage(`${error instanceof Error ? error.message : "Upload failed."} ${accepted
        ? "The file was accepted and is kept in Documents. Do not upload it again."
        : "Retry the same file to check the existing request."}`);
    } finally {
      locked.current = false;
      if (active.current) { setBusy(false); onBusy(false); }
    }
  }

  return <div className="pair-upload">
    <label>Upload {labels[role].toLowerCase()}<input name="file" type="file" accept=".pdf,.png,.jpg,.jpeg,.txt"
      disabled={disabled || busy || separate} onChange={event => {
        const file = event.currentTarget.files?.[0];
        // Clear only the picker, so the same file can be selected again after a failed attempt.
        event.currentTarget.value = "";
        if (file) void upload(file);
      }} /></label>
    <p>Choose a file to upload it. PDF, PNG, JPEG or TXT · maximum 10 MB</p>
    {busy && <p role="status">Uploading {labels[role].toLowerCase()}…</p>}
    {message && <p role="status">{message}</p>}
    {retryFile && !separate && <button type="button" disabled={disabled || busy} onClick={() => void upload(retryFile)}>Retry upload</button>}
    {separate && retryFile && <button type="button" disabled={disabled || busy} onClick={() => {
      void upload(retryFile, true);
    }}>Start a separate upload</button>}
  </div>;
}
