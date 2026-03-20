import { Switch } from "@/components/ui/switch";

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
    <div className="flex items-center justify-between p-3 bg-muted/50 rounded-lg">
      <div>
        <div className="font-medium text-sm">Unilateral Exercise</div>
        <div className="text-xs text-muted-foreground">
          Performed one side at a time (max 3 sets per side)
        </div>
      </div>
      <Switch
        checked={isUnilateral}
        onCheckedChange={onChange}
        disabled={disabled}
      />
    </div>
  );
}
