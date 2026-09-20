import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { createMemoryRouter, RouterProvider } from 'react-router'
import { TodoListRoute } from '../../src/routes/TodoListRoute'
import { todoListLoader } from '../../src/routes/TodoListRoute.loader'
import { todoListAction } from '../../src/routes/TodoListRoute.action'
import { server } from '../mocks/server'
import { sampleTodo } from '../mocks/handlers'

const BASE_URL = import.meta.env.VITE_API_BASE_URL

const renderRoute = () => {
  const router = createMemoryRouter(
    [
      {
        path: '/',
        element: <TodoListRoute />,
        loader: todoListLoader,
        action: todoListAction,
      },
    ],
    { initialEntries: ['/'] },
  )
  render(<RouterProvider router={router} />)
  return router
}

describe('TodoListRoute', () => {
  it('renders the list from the loader', async () => {
    renderRoute()

    expect(await screen.findByText(sampleTodo.title)).toBeInTheDocument()
  })

  it('adds a todo and shows it in the refreshed list after redirect', async () => {
    const user = userEvent.setup()
    renderRoute()

    await screen.findByText(sampleTodo.title)

    await user.type(screen.getByLabelText('Title'), 'Walk the dog')
    await user.click(screen.getByRole('button', { name: /add to-do/i }))

    await waitFor(() => {
      expect(screen.getByText('Walk the dog')).toBeInTheDocument()
    })
  })

  it('clears the form fields after a successful add', async () => {
    const user = userEvent.setup()
    renderRoute()

    await screen.findByText(sampleTodo.title)

    await user.type(screen.getByLabelText('Title'), 'Walk the dog')
    await user.type(screen.getByLabelText('Description'), 'Around the block')
    await user.click(screen.getByRole('button', { name: /add to-do/i }))

    await waitFor(() => {
      expect(screen.getByText('Walk the dog')).toBeInTheDocument()
    })

    expect(screen.getByLabelText('Title')).toHaveValue('')
    expect(screen.getByLabelText('Description')).toHaveValue('')
  })

  it('keeps the form and shows a field error when the server rejects the submission', async () => {
    const user = userEvent.setup()

    server.use(
      http.post(`${BASE_URL}/todos`, () =>
        HttpResponse.json(
          {
            title: 'One or more validation errors occurred.',
            status: 400,
            errors: { DueDate: ['DueDate must not be in the past.'] },
          },
          { status: 400 },
        ),
      ),
    )

    renderRoute()
    await screen.findByText(sampleTodo.title)

    await user.type(screen.getByLabelText('Title'), 'Overdue task')
    await user.click(screen.getByRole('button', { name: /add to-do/i }))

    expect(
      await screen.findByText('DueDate must not be in the past.'),
    ).toBeInTheDocument()
    expect(screen.getByLabelText('Title')).toHaveValue('Overdue task')
  })
})
