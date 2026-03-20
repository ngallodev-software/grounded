import { Button } from '@/components/ui/button'

interface AuthGateProps {
  onUnlock: () => void
}

export function AuthGate({ onUnlock }: AuthGateProps) {
  return (
    <div className="flex flex-col items-center justify-center min-h-screen bg-zinc-950 px-6">
      <div className="w-full max-w-sm space-y-8">
        <div className="text-center space-y-2">
          <h1 className="text-2xl font-[Fraunces] font-light text-zinc-100 tracking-tight">
            Grounded
          </h1>
          <p className="text-xs font-mono text-zinc-600">
            Natural language analytics · Restricted access
          </p>
        </div>

        <div className="border border-zinc-800 bg-zinc-900/50 p-8 space-y-6">
          <div className="space-y-1">
            <div className="flex items-center gap-2">
              <div className="w-2 h-2 rounded-full bg-amber-500/60" />
              <span className="text-xs font-mono text-zinc-500 uppercase tracking-widest">
                Access Required
              </span>
            </div>
            <p className="text-sm text-zinc-400 leading-relaxed pl-4">
              Authentication is required to use this interface.
              Contact your administrator or sign in via Cloudflare Access.
            </p>
          </div>

          <Button
            onClick={onUnlock}
            className="w-full"
          >
            Sign in
          </Button>
        </div>

        <p className="text-center text-[11px] font-mono text-zinc-700">
          Secured by Cloudflare Access
        </p>
      </div>
    </div>
  )
}
