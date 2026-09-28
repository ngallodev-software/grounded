import { useEffect, useState } from 'react'
import { Monitor, Moon, Sun } from 'lucide-react'

type Theme = 'system' | 'light' | 'dark'

function readTheme(): Theme {
  try {
    const stored = window.localStorage.getItem('portfolio-theme')
    return stored === 'light' || stored === 'dark' ? stored : 'system'
  } catch {
    return 'system'
  }
}

export function ThemeToggle() {
  const [theme, setTheme] = useState<Theme>(readTheme)

  useEffect(() => {
    const media = window.matchMedia('(prefers-color-scheme: dark)')

    const apply = () => {
      const dark = theme === 'dark' || (theme === 'system' && media.matches)
      document.documentElement.classList.toggle('dark', dark)
      document.documentElement.style.colorScheme = dark ? 'dark' : 'light'
    }

    apply()

    try {
      window.localStorage.setItem('portfolio-theme', theme)
    } catch {
      // Theme persistence is optional.
    }

    media.addEventListener('change', apply)
    return () => media.removeEventListener('change', apply)
  }, [theme])

  const Icon = theme === 'system' ? Monitor : theme === 'dark' ? Moon : Sun

  return (
    <label className="theme-select">
      <Icon size={15} aria-hidden="true" />
      <span className="sr-only">Colour theme</span>
      <select value={theme} onChange={(event) => setTheme(event.target.value as Theme)}>
        <option value="system">System</option>
        <option value="light">Light</option>
        <option value="dark">Dark</option>
      </select>
    </label>
  )
}
