import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { createMemoryRouter, RouterProvider } from 'react-router'
import { TodoDetailRoute } from '../../src/routes/TodoDetailRoute'
import { todoDetailLoader } from '../../src/routes/TodoDetailRoute.loader'
import { ErrorBoundary } from '../../src/routes/ErrorBoundary'
import { sampleTodo } from '../mocks/handlers'

const renderRoute = (id: string) => {
  const router = createMemoryRouter(
    [
      {
        path: '/todos/:id',
        element: <TodoDetailRoute />,
        loader: todoDetailLoader,
        errorElement: <ErrorBoundary />,
      },
    ],
    { initialEntries: [`/todos/${id}`] },
  )
  render(<RouterProvider router={router} />)
  return router
}

describe('TodoDetailRoute', () => {
  it('renders the todo details from the loader', async () => {
    renderRoute(sampleTodo.id)

    expect(await screen.findByText(sampleTodo.title)).toBeInTheDocument()
    expect(screen.getByText(sampleTodo.description!)).toBeInTheDocument()
  })

  it('renders the error boundary for a todo that does not exist', async () => {
    renderRoute('does-not-exist')

    expect(await screen.findByText('Todo not found')).toBeInTheDocument()
  })

  it('links back to the list', async () => {
    renderRoute(sampleTodo.id)

    await screen.findByText(sampleTodo.title)

    expect(screen.getByRole('link', { name: /back to list/i })).toHaveAttribute('href', '/')
  })
})
