import { defineConfig } from 'orval'

export default defineConfig({
  todoApi: {
    input: '../backend/src/TodoApi.Gateway/openapi.json',
    output: {
      target: 'src/api/generated/todos.ts',
      schemas: 'src/api/generated/models',
      client: 'fetch',
      mode: 'tags-split',
      override: {
        mutator: {
          path: './src/api/mutator/fetchWithBaseUrl.ts',
          name: 'fetchWithBaseUrl',
        },
      },
    },
  },
})
