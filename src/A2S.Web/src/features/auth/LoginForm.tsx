import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';

interface LoginFormProps {
  onSubmit?: (email: string, password: string) => void;
  isLoading?: boolean;
  error?: string;
}

/**
 * Custom login form using ShadCN UI components.
 * Can be used with Clerk or other authentication providers.
 */
export function LoginForm({ onSubmit, isLoading = false, error }: LoginFormProps) {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    onSubmit?.(email, password);
  };

  return (
    <Card className="w-full max-w-md">
      <CardHeader className="space-y-1">
        <CardTitle className="text-2xl font-bold">Sign In</CardTitle>
        <CardDescription>
          Enter your credentials to access your account
        </CardDescription>
      </CardHeader>
      <CardContent>
        <form onSubmit={handleSubmit} className="space-y-4">
          <div className="space-y-2">
            <Label htmlFor="email">Email</Label>
            <Input
              id="email"
              type="email"
              placeholder="name@example.com"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              disabled={isLoading}
              required
            />
          </div>
          <div className="space-y-2">
            <Label htmlFor="password">Password</Label>
            <Input
              id="password"
              type="password"
              placeholder="Enter your password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              disabled={isLoading}
              required
            />
          </div>
          {error && (
            <div className="text-sm text-destructive-foreground bg-destructive/10 border border-destructive rounded-md p-3">
              {error}
            </div>
          )}
          <Button type="submit" className="w-full" disabled={isLoading}>
            {isLoading ? 'Signing in...' : 'Sign In'}
          </Button>
        </form>
        <div className="mt-4 text-center text-sm text-muted-foreground">
          Don't have an account?{' '}
          <a href="/sign-up" className="text-primary hover:underline font-medium">
            Sign up
          </a>
        </div>
      </CardContent>
    </Card>
  );
}
