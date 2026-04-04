import { Eye, EyeOff } from "lucide-react";
import * as React from "react";

import { useI18n } from "@/app/providers/i18n-provider";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { cn } from "@/lib/utils";

type PasswordInputProps = Omit<React.ComponentProps<"input">, "type">;

const PasswordInput = React.forwardRef<HTMLInputElement, PasswordInputProps>(
  ({ className, disabled, ...props }, ref) => {
    const [isVisible, setIsVisible] = React.useState(false);
    const { t } = useI18n();

    return (
      <div className="relative">
        <Input
          ref={ref}
          type={isVisible ? "text" : "password"}
          className={cn("pr-14", className)}
          disabled={disabled}
          {...props}
        />
        <Button
          type="button"
          variant="ghost"
          size="sm"
          className="absolute right-1.5 top-1.5 h-8 rounded-xl px-2.5 text-muted-foreground hover:text-foreground"
          aria-label={isVisible ? t("auth.passwordVisibility.hide") : t("auth.passwordVisibility.show")}
          aria-pressed={isVisible}
          disabled={disabled}
          onClick={() => setIsVisible((value) => !value)}
        >
          {isVisible ? <EyeOff /> : <Eye />}
        </Button>
      </div>
    );
  },
);

PasswordInput.displayName = "PasswordInput";

export { PasswordInput };
