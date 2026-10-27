import { useEffect, useRef, useState } from "react";

type Props = {
  label: string;
  value: string;
  choices: readonly string[];
  onChange: (value: string) => void;
  disabled: boolean;
  required?: boolean;
  name?: string;
  placeholder?: string;
};

export function ReasonInput({ label, value, choices, onChange, disabled, required, name, placeholder }: Props) {
  const input = useRef<HTMLInputElement>(null);
  const focusCustom = useRef(false);
  // Keep an explicitly empty Other field open, but let a saved/discarded draft
  // reset to the prompt when the parent clears a previously non-empty reason.
  const [customValue, setCustomValue] = useState<string | null>(null);
  const custom = customValue === value || (!!value && !choices.includes(value));
  const selection = custom ? "other" : value;

  useEffect(() => {
    if (focusCustom.current && input.current) {
      input.current.focus();
      focusCustom.current = false;
    }
  });

  return <div className="review-reason">
    <label>{label}<select aria-label={`${label} choices`} disabled={disabled} value={selection} required={required}
      onChange={event => {
        const selected = event.target.value;
        if (selected === "other") {
          const reason = custom ? value : "";
          setCustomValue(reason);
          focusCustom.current = true;
          onChange(reason);
        } else {
          setCustomValue(null);
          focusCustom.current = false;
          onChange(selected);
        }
      }}>
      <option value="">Choose a reason</option>
      {choices.map(choice => <option key={choice} value={choice}>{choice}</option>)}
      <option value="other">Other — write my own</option>
    </select></label>
    {custom && <label>Your explanation<input ref={input} aria-label={`${label}: Your explanation`} disabled={disabled} dir="auto" name={name}
      placeholder={placeholder} value={value} onChange={event => {
        setCustomValue(event.target.value);
        onChange(event.target.value);
      }} required={required}/></label>}
  </div>;
}
