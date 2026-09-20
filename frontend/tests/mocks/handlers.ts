import { http, HttpResponse } from 'msw'
import type { TodoResponse } from '../../src/api/generated/models'

const BASE_URL = import.meta.env.VITE_API_BASE_URL

export const sampleTodo: TodoResponse = {
  id: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
  title: 'Buy milk',
  description: '2% or whole, whichever is on sale',
  dueDate: '2026-09-25T00:00:00Z',
  isCompleted: false,
  createdAt: '2026-09-19T14:32:07.1234567Z',
  updatedAt: null,
}

let todos: TodoResponse[] = [sampleTodo]

export const resetTodos = () => {
  todos = [sampleTodo]
}

export const handlers = [
  http.get(`${BASE_URL}/todos`, () => HttpResponse.json(todos)),

  http.get(`${BASE_URL}/todos/:id`, ({ params }) => {
    const todo = todos.find((t) => t.id === params.id)

    if (!todo) {
      return new HttpResponse(null, { status: 404 })
    }

    return HttpResponse.json(todo)
  }),

  http.delete(`${BASE_URL}/todos/:id`, ({ params }) => {
    const exists = todos.some((t) => t.id === params.id)

    if (!exists) {
      return new HttpResponse(null, { status: 404 })
    }

    todos = todos.filter((t) => t.id !== params.id)
    return new HttpResponse(null, { status: 204 })
  }),

  http.post(`${BASE_URL}/todos`, async ({ request }) => {
    const body = (await request.json()) as { title?: string }

    if (!body.title) {
      return HttpResponse.json(
        {
          title: 'One or more validation errors occurred.',
          status: 400,
          errors: { Title: ["'Title' must not be empty."] },
        },
        { status: 400 },
      )
    }

    const created: TodoResponse = {
      ...sampleTodo,
      id: crypto.randomUUID(),
      title: body.title,
    }
    todos = [...todos, created]

    return HttpResponse.json(created, { status: 201 })
  }),
]
