interface UnilateralToggleProps {
  isUnilateral: boolean;
  onChange: (value: boolean) => void;
  disabled?: boolean;
}

export function UnilateralToggle({
  isUnilateral,
  onChange,
  disabled = false,
}: UnilateralToggleProps) {
  return (
    <div>
      <label className="block text-sm font-medium mb-2 text-foreground">Exercise Type</label>
      <div className="grid grid-cols-2 gap-2">
        <button
          type="button"
          disabled={disabled}
          onClick={() => onChange(false)}
          className={`px-3 py-2.5 text-sm font-medium rounded-xl border-2 transition-all ${
            !isUnilateral
              ? "bg-primary border-primary text-primary-foreground font-bold ring-2 ring-primary/50"
              : "border-border hover:border-primary/50 text-foreground"
          } ${disabled ? "opacity-50 cursor-not-allowed" : ""}`}
        >
          Bilateral
        </button>
        <button
          type="button"
          disabled={disabled}
          onClick={() => onChange(true)}
          className={`px-3 py-2.5 text-sm font-medium rounded-xl border-2 transition-all ${
            isUnilateral
              ? "bg-primary border-primary text-primary-foreground font-bold ring-2 ring-primary/50"
              : "border-border hover:border-primary/50 text-foreground"
          } ${disabled ? "opacity-50 cursor-not-allowed" : ""}`}
        >
          Unilateral
        </button>
      </div>
      <p className="text-xs text-muted-foreground mt-2">
        {isUnilateral
          ? "Performed one side at a time (max 3 sets per side)"
          : "Performed with both sides together (max 5 sets)"}
      </p>
    </div>
  );
}
