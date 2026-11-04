import js from '@eslint/js'
import globals from 'globals'
import reactHooks from 'eslint-plugin-react-hooks'
import reactRefresh from 'eslint-plugin-react-refresh'
import tseslint from 'typescript-eslint'

export default [
  { ignores: ['**/dist/**', '**/node_modules/**'] },
  {
    files: ['*.config.{js,ts}', 'scripts/**/*.mjs'],
    languageOptions: {
      parser: tseslint.parser,
      ecmaVersion: 2022,
      globals: globals.node,
    },
    plugins: {
      '@typescript-eslint': tseslint.plugin,
    },
    rules: {
      ...js.configs.recommended.rules,
      ...tseslint.configs.recommended.at(-1).rules,
    },
  },
  {
    files: [
      'src/HexletCalendar.Web/**/*.{ts,tsx}',
      'tests/HexletCalendar.Web.Tests/**/*.{ts,tsx}',
    ],
    languageOptions: {
      parser: tseslint.parser,
      ecmaVersion: 2022,
      globals: globals.browser,
    },
    plugins: {
      '@typescript-eslint': tseslint.plugin,
      'react-hooks': reactHooks,
      'react-refresh': reactRefresh,
    },
    rules: {
      ...js.configs.recommended.rules,
      ...tseslint.configs.recommended.at(-1).rules,
      ...reactHooks.configs.recommended.rules,
      'react-refresh/only-export-components': [
        'warn',
        { allowConstantExport: true },
      ],
      'no-undef': 'off',
    },
  },
]
